using Microsoft.EntityFrameworkCore;

namespace Nomax.BuildingBlocks.Messaging;

/// <summary>Maps the outbox and inbox tables of docs/03 §10 into every module schema.</summary>
internal static class MessagingModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(message => message.Id);
            builder.Property(message => message.Id).ValueGeneratedNever();
            builder.Property(message => message.Type).HasMaxLength(OutboxMessage.TypeMaxLength);
            builder.Property(message => message.Payload).HasColumnType("jsonb");
            builder.Property(message => message.Attempts).HasDefaultValueSql("0").ValueGeneratedNever();
            builder.HasIndex(message => message.OccurredAt)
                .HasDatabaseName("ix_outbox_messages_pending")
                .HasFilter("processed_at IS NULL AND failed_at IS NULL");
        });

        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("inbox_messages");
            builder.HasKey(message => new { message.MessageId, message.Handler });
            builder.Property(message => message.Handler).HasMaxLength(InboxMessage.HandlerMaxLength);
        });
    }
}
