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

namespace Nomax.Modules.Content.Features.Books;

/// <param name="CoverFileId">A ready <c>book_cover</c> upload (BR-FILE-06: admin only).</param>
public sealed record CreateBookRequest(Guid LevelId, TextBlock Title, int SortOrder, Guid? CoverFileId = null);

internal sealed class CreateBookValidator : AbstractValidator<CreateBookRequest>
{
    public CreateBookValidator()
    {
        RuleFor(request => request.LevelId).NotEmpty();
        RuleFor(request => request.Title).ValidTextBlock(TextBlockUsage.Title);
        RuleFor(request => request.SortOrder).GreaterThanOrEqualTo(0);
    }
}

/// <summary>POST /api/content/books — admin (BR-KONTEN-03).</summary>
internal sealed class CreateBookHandler(ContentDbContext db, CoverFiles coverFiles, TimeProvider timeProvider)
{
    public async Task<Result<BookDto>> HandleAsync(CreateBookRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Levels.AnyAsync(level => level.Id == request.LevelId, cancellationToken))
        {
            return ContentErrors.UnknownReference("levelId");
        }

        if (request.CoverFileId is { } coverFileId && await coverFiles.CheckAsync(coverFileId, cancellationToken) is { } coverError)
        {
            return coverError;
        }

        var book = Book.Create(Uuid7.New(timeProvider), request.LevelId, request.Title, request.CoverFileId, request.SortOrder);
        db.Books.Add(book);
        await db.SaveChangesAsync(cancellationToken);
        return BookDto.From(book, []);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/books", async (CreateBookRequest request, CreateBookHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken))
                    .ToHttpResult(book => TypedResults.Created($"/api/content/books/{book.Id}", book)))
            .RequireAuthorization(Policies.AdminOnly)
            .WithValidation<CreateBookRequest>()
            .WithName("CreateBook")
            .WithSummary("Create a book in a level (admin)")
            .Produces<BookDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
