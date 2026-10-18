using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nomax.Modules.Classroom.Domain;
using Nomax.Modules.Identity.Contracts;
using Nomax.SharedKernel.Serialization;

namespace Nomax.Modules.Classroom.Infrastructure.Notifications;

/// <summary>Sends one email through SMTP (Mailpit in development).</summary>
internal sealed class EmailSender(IOptions<EmailOptions> options)
{
    public bool IsEnabled => options.Value.Enabled && !string.IsNullOrWhiteSpace(options.Value.SmtpHost);

    public async Task SendAsync(string to, RenderedMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var mail = new MailMessage
        {
            From = new MailAddress(settings.From),
            Subject = message.Subject,
            Body = message.Text,
            IsBodyHtml = false,
        };
        mail.To.Add(to);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.Html, null, "text/html"));

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort) { EnableSsl = settings.UseSsl };
        if (!string.IsNullOrEmpty(settings.Username))
        {
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        }

        await client.SendMailAsync(mail, cancellationToken);
    }
}

/// <summary>
/// Job of docs/02 §11 (every 30 seconds): sends due notifications. Rows are taken with <c>FOR UPDATE SKIP LOCKED</c> so
/// several API instances never send twice. Inactive users and reminders of cancelled sessions are cancelled instead;
/// a push subscription the push service no longer knows is deleted. Failures are retried with growing delays.
/// </summary>
internal sealed partial class NotificationDispatcher(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<NotificationDispatcher> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private const int MaxAttempts = 5;

    private const string SelectDue =
        "SELECT * FROM classroom.notifications WHERE status = 'pending' AND send_at <= now() ORDER BY send_at LIMIT 100 FOR UPDATE SKIP LOCKED";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), timeProvider);
        do
        {
            try
            {
                while (await DispatchBatchAsync(stoppingToken) == BatchSize)
                {
                    // A full batch: there may be more due right now.
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                LogBatchFailed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ClassroomDbContext>();
        var users = services.GetRequiredService<IUserDirectory>();
        var email = services.GetRequiredService<EmailSender>();
        var push = services.GetRequiredService<WebPushSender>();
        var appUrl = services.GetRequiredService<IOptions<AppOptions>>().Value.PublicUrl.TrimEnd('/');

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var due = await db.Notifications.FromSqlRaw(SelectDue).ToListAsync(cancellationToken);
        if (due.Count == 0)
        {
            return 0;
        }

        var sessionIds = due.Select(notification => notification.Payload.SessionId).Distinct().ToList();
        var sessions = await db.ClassSessions.AsNoTracking().Where(session => sessionIds.Contains(session.Id)).ToDictionaryAsync(session => session.Id, cancellationToken);
        var classIds = sessions.Values.Select(session => session.ClassId).Distinct().ToList();
        var classNames = await db.Classes.AsNoTracking().Where(item => classIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var recipients = await users.GetUsersAsync([.. due.Select(notification => notification.UserId).Distinct()], cancellationToken);
        var pushUserIds = due.Where(notification => notification.Channel == NotificationChannel.WebPush).Select(notification => notification.UserId).Distinct().ToList();
        var subscriptions = (await db.PushSubscriptions.Where(subscription => pushUserIds.Contains(subscription.UserId)).ToListAsync(cancellationToken))
            .ToLookup(subscription => subscription.UserId);

        foreach (var notification in due)
        {
            var now = timeProvider.GetUtcNow();
            if (!recipients.TryGetValue(notification.UserId, out var user) || !user.IsActive)
            {
                notification.Cancel("Recipient inactive.");
                continue;
            }

            if (!sessions.TryGetValue(notification.Payload.SessionId, out var session)
                || (session.Status == SessionStatus.Cancelled && NotificationTypes.IsReminder(notification.Type)))
            {
                notification.Cancel("Session cancelled.");
                continue;
            }

            var message = NotificationTemplates.Render(
                notification.Type,
                user.UiLocale,
                user.Timezone,
                new SessionMessageData(
                    classNames.GetValueOrDefault(session.ClassId) ?? string.Empty,
                    session.Topic,
                    session.StartsAt,
                    session.EndsAt,
                    EnumSnakeCase.ToValue(session.Platform),
                    session.MeetingUrl,
                    notification.Payload.Previous,
                    $"{appUrl}/murid/jadwal"));

            try
            {
                if (notification.Channel == NotificationChannel.Email)
                {
                    if (!email.IsEnabled || user.Email is null)
                    {
                        notification.Cancel("Email is not available.");
                        continue;
                    }

                    await email.SendAsync(user.Email, message, cancellationToken);
                    notification.MarkSent(now);
                }
                else
                {
                    await SendPushAsync(db, push, notification, subscriptions[notification.UserId].ToList(), message, $"{appUrl}/murid/jadwal", now, cancellationToken);
                }
            }
            catch (Exception exception) when (exception is SmtpException or HttpRequestException or InvalidOperationException or IOException or TaskCanceledException)
            {
                notification.RecordFailure(exception.Message, now.AddMinutes(5 * (notification.Attempts + 1)), MaxAttempts);
                LogSendFailed(logger, notification.Id, notification.Channel, exception);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return due.Count;
    }

    private static async Task SendPushAsync(
        ClassroomDbContext db,
        WebPushSender push,
        Notification notification,
        List<PushSubscription> subscriptions,
        RenderedMessage message,
        string url,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!push.IsEnabled || subscriptions.Count == 0)
        {
            notification.Cancel("No push subscription.");
            return;
        }

        var payload = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["title"] = message.PushTitle,
            ["body"] = message.PushBody,
            ["url"] = url,
        });

        var delivered = false;
        string? lastError = null;
        foreach (var subscription in subscriptions)
        {
            var (result, error) = await push.SendAsync(subscription.Endpoint, subscription.P256dh, subscription.Auth, payload, cancellationToken);
            switch (result)
            {
                case PushResult.Delivered:
                    subscription.MarkDelivered(now);
                    delivered = true;
                    break;
                case PushResult.Gone:
                    db.PushSubscriptions.Remove(subscription);
                    break;
                default:
                    lastError = error;
                    break;
            }
        }

        if (delivered)
        {
            notification.MarkSent(now);
        }
        else if (lastError is null)
        {
            notification.Cancel("No valid push subscription.");
        }
        else
        {
            notification.RecordFailure(lastError, now.AddMinutes(5 * (notification.Attempts + 1)), MaxAttempts);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification batch failed")]
    private static partial void LogBatchFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification {NotificationId} ({Channel}) could not be sent")]
    private static partial void LogSendFailed(ILogger logger, Guid notificationId, NotificationChannel channel, Exception exception);
}
