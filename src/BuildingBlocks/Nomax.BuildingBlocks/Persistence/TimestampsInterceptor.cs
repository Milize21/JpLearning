using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Nomax.BuildingBlocks.Persistence;

/// <summary>
/// Sets <c>created_at</c> and <c>updated_at</c> from <see cref="TimeProvider"/> so tests control the clock.
/// </summary>
public sealed class TimestampsInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public static void ApplyTimestamps(ChangeTracker changeTracker, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(changeTracker);
        foreach (var entry in changeTracker.Entries<IHasTimestamps>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(IHasTimestamps.CreatedAt)).CurrentValue = now;
                    entry.Property(nameof(IHasTimestamps.UpdatedAt)).CurrentValue = now;
                    break;
                case EntityState.Modified:
                    entry.Property(nameof(IHasTimestamps.UpdatedAt)).CurrentValue = now;
                    entry.Property(nameof(IHasTimestamps.CreatedAt)).IsModified = false;
                    break;
            }
        }
    }

    private void Apply(DbContextEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context)
        {
            ApplyTimestamps(context.ChangeTracker, timeProvider.GetUtcNow());
        }
    }
}
