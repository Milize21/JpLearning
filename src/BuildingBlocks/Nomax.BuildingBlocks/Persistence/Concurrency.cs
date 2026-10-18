using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nomax.SharedKernel.Results;

namespace Nomax.BuildingBlocks.Persistence;

/// <summary>
/// Entity edited by several people (content, questions, quizzes, assignments, grade schemes) with PostgreSQL's
/// <c>xmin</c> as concurrency token (docs/03 §1 rule 12). The API returns it as <c>version</c> and PATCH/PUT must send it back.
/// </summary>
public interface IHasVersion
{
    uint Version { get; }
}

public static class Concurrency
{
    /// <summary>Maps <see cref="IHasVersion.Version"/> to the <c>xmin</c> system column (no column is created).</summary>
    public static EntityTypeBuilder<T> HasXminVersion<T>(this EntityTypeBuilder<T> builder)
        where T : class, IHasVersion
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Property(entity => entity.Version).IsRowVersion();
        return builder;
    }

    /// <summary>
    /// The client must still have the current version: a different one is 409 <c>concurrency_conflict</c> at once (also when
    /// the request changes nothing), and the original value makes a concurrent change between load and save fail too.
    /// </summary>
    public static AppError? ExpectVersion<T>(this DbContext db, T entity, uint version)
        where T : class, IHasVersion
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.Version != version)
        {
            return Conflict();
        }

        db.Entry(entity).Property(nameof(IHasVersion.Version)).OriginalValue = version;
        return null;
    }

    /// <summary>Saves; a changed version becomes 409 <c>concurrency_conflict</c> (docs/05 §1).</summary>
    public static async Task<AppError?> SaveChangesOrConflictAsync(this DbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
    }

    private static AppError Conflict() =>
        new(ErrorCodes.ConcurrencyConflict, "The data was changed by someone else. Reload and try again.");
}
