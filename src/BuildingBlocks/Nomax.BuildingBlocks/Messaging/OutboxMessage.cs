namespace Nomax.BuildingBlocks.Messaging;

/// <summary>
/// <c>{schema}.outbox_messages</c> (docs/03 §10): an integration event saved in the same transaction as the change
/// that caused it, and sent afterwards by <see cref="OutboxProcessor{TContext}"/>. Infrastructure table, so no
/// created_at/updated_at.
/// </summary>
public sealed class OutboxMessage
{
    public const int TypeMaxLength = 200;

    private OutboxMessage()
    {
    }

    internal OutboxMessage(Guid id, string type, string payload, DateTimeOffset occurredAt)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredAt = occurredAt;
    }

    /// <summary>Also the <c>eventId</c> of the envelope.</summary>
    public Guid Id { get; private set; }

    public string Type { get; private set; } = string.Empty;

    /// <summary>The event record as JSON (camelCase, snake_case enums), stored as jsonb.</summary>
    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>Set after the last allowed attempt failed; the message is then left alone.</summary>
    public DateTimeOffset? FailedAt { get; private set; }

    internal void MarkProcessed(DateTimeOffset now) => ProcessedAt = now;

    internal void RecordFailure(string error, DateTimeOffset now, int maxAttempts)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;
        if (Attempts >= maxAttempts)
        {
            FailedAt = now;
        }
    }
}

/// <summary>
/// <c>{schema}.inbox_messages</c> (docs/03 §10): one row per event and consumer handler, written in the same transaction
/// as the handler's effects, so a redelivered event is ignored.
/// </summary>
public sealed class InboxMessage
{
    public const int HandlerMaxLength = 200;

    private InboxMessage()
    {
    }

    internal InboxMessage(Guid messageId, string handler, DateTimeOffset processedAt)
    {
        MessageId = messageId;
        Handler = handler;
        ProcessedAt = processedAt;
    }

    public Guid MessageId { get; private set; }

    public string Handler { get; private set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; private set; }
}
