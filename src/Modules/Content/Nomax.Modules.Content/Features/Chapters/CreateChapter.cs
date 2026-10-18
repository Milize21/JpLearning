using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Content.Domain;
using Nomax.Modules.Content.Features.Common;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Features.Chapters;

/// <param name="Number">Chapter number shown to users, unique within the book.</param>
public sealed record CreateChapterRequest(Guid BookId, int Number, TextBlock Title, int SortOrder);

internal sealed class CreateChapterValidator : AbstractValidator<CreateChapterRequest>
{
    public CreateChapterValidator()
    {
        RuleFor(request => request.BookId).NotEmpty();
        RuleFor(request => request.Number).InclusiveBetween(1, ContentRules.MaxChapterNumber);
        RuleFor(request => request.Title).ValidTextBlock(TextBlockUsage.Title);
        RuleFor(request => request.SortOrder).GreaterThanOrEqualTo(0);
    }
}

/// <summary>POST /api/content/chapters — admin (BR-KONTEN-03).</summary>
internal sealed class CreateChapterHandler(ContentDbContext db, TimeProvider timeProvider)
{
    public async Task<Result<ChapterDto>> HandleAsync(CreateChapterRequest request, CancellationToken cancellationToken)
    {
        var levelId = await StructureQueries.LevelOfBookAsync(db, request.BookId, cancellationToken);
        if (levelId == Guid.Empty)
        {
            return ContentErrors.UnknownReference("bookId");
        }

        if (await db.Chapters.AnyAsync(chapter => chapter.BookId == request.BookId && chapter.Number == request.Number, cancellationToken))
        {
            return AppError.Conflict("The book already has a chapter with this number.");
        }

        var chapter = Chapter.Create(Uuid7.New(timeProvider), request.BookId, request.Number, request.Title, request.SortOrder);
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync(cancellationToken);
        return ChapterDto.From(chapter, levelId);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/chapters", async (CreateChapterRequest request, CreateChapterHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken))
                    .ToHttpResult(chapter => TypedResults.Created($"/api/content/chapters/{chapter.Id}", chapter)))
            .RequireAuthorization(Policies.AdminOnly)
            .WithValidation<CreateChapterRequest>()
            .WithName("CreateChapter")
            .WithSummary("Create a chapter in a book (admin)")
            .Produces<ChapterDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
