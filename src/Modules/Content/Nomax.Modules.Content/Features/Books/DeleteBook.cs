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

namespace Nomax.Modules.Content.Features.Books;

/// <summary>DELETE /api/content/books/{id} — admin; BR-KONTEN-03: no chapters and no class using the book.</summary>
internal sealed class DeleteBookHandler(ContentDbContext db, ContentUsage usage)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await db.Books.FirstOrDefaultAsync(book => book.Id == id, cancellationToken);
        if (book is null)
        {
            return AppError.NotFound();
        }

        if (await db.Chapters.AnyAsync(chapter => chapter.BookId == id, cancellationToken)
            || await usage.IsUsedElsewhereAsync(ContentUsageTarget.Book, id, cancellationToken))
        {
            return ContentErrors.HasDependents("book");
        }

        db.Books.Remove(book);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapDelete("/books/{id:guid}", async (Guid id, DeleteBookHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.AdminOnly)
            .WithName("DeleteBook")
            .WithSummary("Delete an unused book (admin)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
