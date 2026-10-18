using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nomax.BuildingBlocks.Persistence;
using Nomax.SharedKernel.Serialization;

namespace Nomax.BuildingBlocks.Messaging;

/// <summary>Configuration section <c>Messaging</c>.</summary>
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    /// <summary>docs/02 §11: the outbox is polled every second.</summary>
    public int OutboxIntervalMs { get; set; } = 1000;

    public int BatchSize { get; set; } = 50;

    /// <summary>docs/02 §6.3 step 4: after this many failures the message is marked failed.</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>Processed outbox and inbox rows are kept this long (docs/03 §14).</summary>
    public int RetentionDays { get; set; } = 14;
}

/// <summary>Sends an outbox message to every in-process handler of its type; each handler runs in its own scope.</summary>
internal sealed class IntegrationEventDispatcher(
    IServiceScopeFactory scopeFactory,
    IEnumerable<IntegrationEventConsumerRegistration> registrations)
{
    private readonly ILookup<string, IntegrationEventConsumerRegistration> _handlers =
        registrations.ToLookup(registration => registration.EventType, StringComparer.Ordinal);

    public bool HasHandlers(string eventType) => _handlers[eventType].Any();

    public async Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        object? payload = null;
        foreach (var registration in _handlers[message.Type])
        {
            payload ??= JsonSerializer.Deserialize(message.Payload, registration.PayloadType, NomaxJson.Options)
                ?? throw new InvalidOperationException($"Outbox message {message.Id} has an empty payload.");
            await using var scope = scopeFactory.CreateAsyncScope();
            await registration.Invoke(scope.ServiceProvider, payload, message.Id, message.OccurredAt, cancellationToken);
        }
    }
}

/// <summary>
/// docs/02 §6.3: one per module DbContext. Takes pending messages with <c>FOR UPDATE SKIP LOCKED</c> (safe with several
/// API instances), dispatches them, and retries failures with exponential backoff until <see cref="MessagingOptions.MaxAttempts"/>.
/// Messages without a handler are marked processed at once (step 5).
/// </summary>
internal sealed partial class OutboxProcessor<TContext>(
    IServiceScopeFactory scopeFactory,
    IntegrationEventDispatcher dispatcher,
    IOptions<MessagingOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor<TContext>> logger) : BackgroundService
    where TContext : ModuleDbContext
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    // The backoff schedule is kept in memory: after a restart pending messages are simply tried again.
    private readonly Dictionary<Guid, DateTimeOffset> _retryAt = [];
    private string? _selectSql;
    private DateTimeOffset _nextCleanup;
    private DateTimeOffset _lastErrorLog;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Max(50, options.Value.OutboxIntervalMs)), timeProvider);
        do
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                // A failing background service would stop the whole host. Database down or not migrated yet:
                // log at most once a minute instead of every second, and keep polling.
                var now = timeProvider.GetUtcNow();
                if (now - _lastErrorLog > TimeSpan.FromMinutes(1))
                {
                    _lastErrorLog = now;
                    LogBatchFailed(logger, typeof(TContext).Name, exception);
                }
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        _selectSql ??= string.Concat(
            "SELECT * FROM \"", db.Model.GetDefaultSchema(), "\".outbox_messages ",
            "WHERE processed_at IS NULL AND failed_at IS NULL ORDER BY occurred_at LIMIT ",
            options.Value.BatchSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            " FOR UPDATE SKIP LOCKED");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var messages = await db.OutboxMessages.FromSqlRaw(_selectSql).ToListAsync(cancellationToken);
        if (messages.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var message in messages)
        {
            if (_retryAt.TryGetValue(message.Id, out var due) && due > now)
            {
                continue;
            }

            try
            {
                if (dispatcher.HasHandlers(message.Type))
                {
                    await dispatcher.DispatchAsync(message, cancellationToken);
                }

                message.MarkProcessed(timeProvider.GetUtcNow());
                _retryAt.Remove(message.Id);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                message.RecordFailure(exception.Message, now, options.Value.MaxAttempts);
                if (message.FailedAt is not null)
                {
                    _retryAt.Remove(message.Id);
                    LogGaveUp(logger, message.Type, message.Id, message.Attempts, exception);
                }
                else
                {
                    var backoff = TimeSpan.FromSeconds(Math.Pow(2, message.Attempts));
                    _retryAt[message.Id] = now + (backoff < MaxBackoff ? backoff : MaxBackoff);
                    LogRetry(logger, message.Type, message.Id, message.Attempts, exception);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (now < _nextCleanup)
        {
            return;
        }

        _nextCleanup = now.AddHours(1);
        var cutoff = now.AddDays(-options.Value.RetentionDays);
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        await db.OutboxMessages.Where(message => message.ProcessedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
        await db.InboxMessages.Where(message => message.ProcessedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox of {Context} could not be processed")]
    private static partial void LogBatchFailed(ILogger logger, string context, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Integration event {Type} {MessageId} failed (attempt {Attempts}); retrying later")]
    private static partial void LogRetry(ILogger logger, string type, Guid messageId, int attempts, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Integration event {Type} {MessageId} failed {Attempts} times and is marked failed")]
    private static partial void LogGaveUp(ILogger logger, string type, Guid messageId, int attempts, Exception exception);
}
