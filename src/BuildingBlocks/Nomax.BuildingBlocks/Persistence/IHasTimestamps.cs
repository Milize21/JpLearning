namespace Nomax.BuildingBlocks.Persistence;

/// <summary>
/// Entity with <c>created_at</c>/<c>updated_at</c> (every table except the infrastructure tables, docs/03 §1 rule 4).
/// Both are filled by <see cref="TimestampsInterceptor"/> and default to <c>now()</c> in the database.
/// </summary>
public interface IHasTimestamps
{
    DateTimeOffset CreatedAt { get; }

    DateTimeOffset UpdatedAt { get; }
}
