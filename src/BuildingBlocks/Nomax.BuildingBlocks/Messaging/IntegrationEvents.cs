using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nomax.BuildingBlocks.Persistence;
using Nomax.SharedKernel.Events;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Serialization;

namespace Nomax.BuildingBlocks.Messaging;

/// <summary>The envelope of docs/05 §8 as a consumer sees it.</summary>
public sealed record IntegrationEventEnvelope<TEvent>(Guid EventId, DateTimeOffset OccurredAt, TEvent Payload)
    where TEvent : IIntegrationEvent;

/// <summary>Consumer of an integration event. Implementations must be idempotent; <see cref="InboxConsumer{TContext,TEvent}"/> does that.</summary>
public interface IIntegrationEventConsumer<TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(IntegrationEventEnvelope<TEvent> envelope, CancellationToken cancellationToken);
}

/// <summary>
/// Handler base that records the event in the consumer module's inbox in the same transaction as its effects
/// (docs/02 §6.3 step 3), so delivering the same event twice changes nothing.
/// </summary>
public abstract class InboxConsumer<TContext, TEvent>(TContext db, TimeProvider timeProvider) : IIntegrationEventConsumer<TEvent>
    where TContext : ModuleDbContext
    where TEvent : IIntegrationEvent
{
    protected TContext Db => db;

    protected TimeProvider Clock => timeProvider;

    public async Task HandleAsync(IntegrationEventEnvelope<TEvent> envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var handler = GetType().FullName ?? GetType().Name;
        if (handler.Length > InboxMessage.HandlerMaxLength)
        {
            handler = handler[^InboxMessage.HandlerMaxLength..];
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await db.InboxMessages.AnyAsync(
                message => message.MessageId == envelope.EventId && message.Handler == handler,
                cancellationToken))
        {
            return;
        }

        await ApplyAsync(envelope, cancellationToken);
        db.InboxMessages.Add(new InboxMessage(envelope.EventId, handler, timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>The effects of the event. Change entities of <see cref="Db"/>; the base class saves and commits.</summary>
    protected abstract Task ApplyAsync(IntegrationEventEnvelope<TEvent> envelope, CancellationToken cancellationToken);
}

public static class OutboxExtensions
{
    /// <summary>
    /// Queues an integration event in the module's outbox. It is written by the next <c>SaveChanges</c>, in the same
    /// transaction as the change that caused it (docs/02 §6.3 step 1).
    /// </summary>
    public static void AddIntegrationEvent<TEvent>(this ModuleDbContext db, TEvent integrationEvent, TimeProvider timeProvider)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(timeProvider);
        var now = timeProvider.GetUtcNow();
        db.OutboxMessages.Add(new OutboxMessage(
            Uuid7.New(timeProvider),
            TEvent.EventType,
            JsonSerializer.Serialize(integrationEvent, NomaxJson.Options),
            now));
    }
}

/// <summary>One consumer handler of one event type, captured as a closed delegate so dispatch needs no reflection.</summary>
internal sealed record IntegrationEventConsumerRegistration(
    string EventType,
    Type PayloadType,
    string ConsumerName,
    Func<IServiceProvider, object, Guid, DateTimeOffset, CancellationToken, Task> Invoke);

public static class IntegrationEventRegistration
{
    /// <summary>Subscribes <typeparamref name="TConsumer"/> (scoped) to <typeparamref name="TEvent"/> in-process (D-03).</summary>
    public static IServiceCollection AddIntegrationEventConsumer<TEvent, TConsumer>(this IServiceCollection services)
        where TEvent : IIntegrationEvent
        where TConsumer : class, IIntegrationEventConsumer<TEvent>
    {
        services.AddScoped<TConsumer>();
        services.AddSingleton(new IntegrationEventConsumerRegistration(
            TEvent.EventType,
            typeof(TEvent),
            typeof(TConsumer).FullName ?? typeof(TConsumer).Name,
            (provider, payload, eventId, occurredAt, cancellationToken) => provider.GetRequiredService<TConsumer>()
                .HandleAsync(new IntegrationEventEnvelope<TEvent>(eventId, occurredAt, (TEvent)payload), cancellationToken)));
        return services;
    }
}
