using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Content.Domain;
using Nomax.Modules.Content.Features.Common;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Serialization;

namespace Nomax.Modules.Content.Features.Reference;

/// <summary>
/// DELETE /api/content/reference/{type}/{id} — admin. Items used by questions (or, for grammar points, by sentences)
/// cannot be deleted: 409 has_dependents.
/// </summary>
internal sealed class DeleteReferenceHandler(ContentDbContext db, ContentUsage usage)
{
    public async Task<Result> HandleAsync(ReferenceType type, Guid id, CancellationToken cancellationToken)
    {
        ReferenceItem? item = type switch
        {
            ReferenceType.Kana => await db.KanaChars.FirstOrDefaultAsync(item => item.Id == id, cancellationToken),
            ReferenceType.Kanji => await db.Kanji.FirstOrDefaultAsync(item => item.Id == id, cancellationToken),
            ReferenceType.Vocab => await db.Vocab.FirstOrDefaultAsync(item => item.Id == id, cancellationToken),
            ReferenceType.Grammar => await db.GrammarPoints.FirstOrDefaultAsync(item => item.Id == id, cancellationToken),
            _ => await db.Sentences.FirstOrDefaultAsync(item => item.Id == id, cancellationToken),
        };
        if (item is null)
        {
            return AppError.NotFound();
        }

        if ((type == ReferenceType.Grammar && await db.Sentences.AnyAsync(sentence => sentence.GrammarPointId == id, cancellationToken))
            || await usage.IsUsedElsewhereAsync(ContentUsageTarget.ReferenceItem, id, cancellationToken))
        {
            return ContentErrors.HasDependents("reference item");
        }

        db.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapDelete("/reference/{type}/{id:guid}", async (string type, Guid id, DeleteReferenceHandler handler, CancellationToken cancellationToken) =>
                EnumSnakeCase.TryParse<ReferenceType>(type, out var referenceType)
                    ? (await handler.HandleAsync(referenceType, id, cancellationToken)).ToHttpResult()
                    : AppError.NotFound("Unknown reference data type.").ToProblem())
            .RequireAuthorization(Policies.AdminOnly)
            .WithName("DeleteReference")
            .WithSummary("Delete a reference data item that no question uses (admin)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
