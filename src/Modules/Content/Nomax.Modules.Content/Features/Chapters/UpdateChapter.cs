using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Persistence;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Content.Domain;
using Nomax.Modules.Content.Features.Common;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Features.Chapters;

public sealed record UpdateChapterRequest(uint Version, int? Number = null, TextBlock? Title = null, int? SortOrder = null);

internal sealed class UpdateChapterValidator : AbstractValidator<UpdateChapterRequest>
{
    public UpdateChapterValidator()
    {
        RuleFor(request => request.Number).InclusiveBetween(1, ContentRules.MaxChapterNumber).When(request => request.Number is not null);
        RuleFor(request => request.Title).ValidTextBlock(TextBlockUsage.Title).When(request => request.Title is not null);
        RuleFor(request => request.SortOrder).GreaterThanOrEqualTo(0).When(request => request.SortOrder is not null);
    }
}

/// <summary>PATCH /api/content/chapters/{id} — admin.</summary>
internal sealed class UpdateChapterHandler(ContentDbContext db)
{
    public async Task<Result<ChapterDto>> HandleAsync(Guid id, UpdateChapterRequest request, CancellationToken cancellationToken)
    {
        var chapter = await db.Chapters.FirstOrDefaultAsync(chapter => chapter.Id == id, cancellationToken);
        if (chapter is null)
        {
            return AppError.NotFound();
        }

        if (request.Number is { } number && number != chapter.Number
            && await db.Chapters.AnyAsync(other => other.BookId == chapter.BookId && other.Number == number, cancellationToken))
        {
            return AppError.Conflict("The book already has a chapter with this number.");
        }

        if (db.ExpectVersion(chapter, request.Version) is { } stale)
        {
            return stale;
        }

        chapter.Update(request.Number, request.Title, request.SortOrder);
        if (await db.SaveChangesOrConflictAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return ChapterDto.From(chapter, await StructureQueries.LevelOfBookAsync(db, chapter.BookId, cancellationToken));
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPatch("/chapters/{id:guid}", async (Guid id, UpdateChapterRequest request, UpdateChapterHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.AdminOnly)
            .WithValidation<UpdateChapterRequest>()
            .WithName("UpdateChapter")
            .WithSummary("Change a chapter (admin)")
            .Produces<ChapterDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
