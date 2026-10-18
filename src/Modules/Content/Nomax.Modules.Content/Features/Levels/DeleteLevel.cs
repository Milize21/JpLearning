using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Content.Features.Common;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Content.Features.Levels;

/// <summary>
/// DELETE /api/content/levels/{id} — admin. BR-KONTEN-03: only a level that was never used: no books or reference data
/// below it and no class or question referring to it (asked through <see cref="IContentUsageProbe"/>).
/// </summary>
internal sealed class DeleteLevelHandler(ContentDbContext db, ContentUsage usage)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var level = await db.Levels.FirstOrDefaultAsync(level => level.Id == id, cancellationToken);
        if (level is null)
        {
            return AppError.NotFound();
        }

        var hasDependents =
            await db.Books.AnyAsync(book => book.LevelId == id, cancellationToken)
            || await db.KanaChars.AnyAsync(item => item.LevelId == id, cancellationToken)
            || await db.Kanji.AnyAsync(item => item.LevelId == id, cancellationToken)
            || await db.Vocab.AnyAsync(item => item.LevelId == id, cancellationToken)
            || await db.GrammarPoints.AnyAsync(item => item.LevelId == id, cancellationToken)
            || await db.Sentences.AnyAsync(item => item.LevelId == id, cancellationToken)
            || await usage.IsUsedElsewhereAsync(ContentUsageTarget.Level, id, cancellationToken);
        if (hasDependents)
        {
            return ContentErrors.HasDependents("level");
        }

        db.Levels.Remove(level);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapDelete("/levels/{id:guid}", async (Guid id, DeleteLevelHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.AdminOnly)
            .WithName("DeleteLevel")
            .WithSummary("Delete an unused level (admin)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
