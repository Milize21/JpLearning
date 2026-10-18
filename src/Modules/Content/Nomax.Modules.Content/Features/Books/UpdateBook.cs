using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Persistence;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Content.Features.Common;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Features.Books;

/// <param name="CoverFileId">New cover; send <see cref="RemoveCover"/> = true to remove it.</param>
public sealed record UpdateBookRequest(uint Version, TextBlock? Title = null, int? SortOrder = null, Guid? CoverFileId = null, bool RemoveCover = false);

internal sealed class UpdateBookValidator : AbstractValidator<UpdateBookRequest>
{
    public UpdateBookValidator()
    {
        RuleFor(request => request.Title).ValidTextBlock(TextBlockUsage.Title).When(request => request.Title is not null);
        RuleFor(request => request.SortOrder).GreaterThanOrEqualTo(0).When(request => request.SortOrder is not null);
    }
}

/// <summary>PATCH /api/content/books/{id} — admin.</summary>
internal sealed class UpdateBookHandler(ContentDbContext db, CoverFiles coverFiles)
{
    public async Task<Result<BookSummaryDto>> HandleAsync(Guid id, UpdateBookRequest request, CancellationToken cancellationToken)
    {
        var book = await db.Books.FirstOrDefaultAsync(book => book.Id == id, cancellationToken);
        if (book is null)
        {
            return AppError.NotFound();
        }

        if (request.CoverFileId is { } coverFileId && await coverFiles.CheckAsync(coverFileId, cancellationToken) is { } coverError)
        {
            return coverError;
        }

        if (db.ExpectVersion(book, request.Version) is { } stale)
        {
            return stale;
        }

        book.Update(request.Title, request.SortOrder);
        if (request.RemoveCover)
        {
            book.ChangeCover(null);
        }
        else if (request.CoverFileId is not null)
        {
            book.ChangeCover(request.CoverFileId);
        }

        if (await db.SaveChangesOrConflictAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        var chapterCount = await db.Chapters.CountAsync(chapter => chapter.BookId == id, cancellationToken);
        return BookSummaryDto.From(book, chapterCount);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPatch("/books/{id:guid}", async (Guid id, UpdateBookRequest request, UpdateBookHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.AdminOnly)
            .WithValidation<UpdateBookRequest>()
            .WithName("UpdateBook")
            .WithSummary("Change a book (admin)")
            .Produces<BookSummaryDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
