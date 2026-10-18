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

namespace Nomax.Modules.Content.Features.Levels;

public sealed record CreateLevelRequest(string Code, TextBlock Name, int SortOrder);

internal sealed class CreateLevelValidator : AbstractValidator<CreateLevelRequest>
{
    public CreateLevelValidator()
    {
        RuleFor(request => request.Code).Must(ContentRules.IsValidLevelCode).WithMessage("Must be 1–16 characters of a-z, 0-9 or hyphen.");
        RuleFor(request => request.Name).ValidTextBlock(TextBlockUsage.Title);
        RuleFor(request => request.SortOrder).GreaterThanOrEqualTo(0);
    }
}

/// <summary>POST /api/content/levels — admin (BR-KONTEN-03).</summary>
internal sealed class CreateLevelHandler(ContentDbContext db, TimeProvider timeProvider)
{
    public async Task<Result<LevelDto>> HandleAsync(CreateLevelRequest request, CancellationToken cancellationToken)
    {
        if (await db.Levels.AnyAsync(level => level.Code == request.Code, cancellationToken))
        {
            return AppError.Conflict("A level with this code already exists.");
        }

        var level = Level.Create(Uuid7.New(timeProvider), request.Code, request.Name, request.SortOrder);
        db.Levels.Add(level);
        await db.SaveChangesAsync(cancellationToken);
        return LevelDto.From(level, []);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/levels", async (CreateLevelRequest request, CreateLevelHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken))
                    .ToHttpResult(level => TypedResults.Created($"/api/content/levels/{level.Id}", level)))
            .RequireAuthorization(Policies.AdminOnly)
            .WithValidation<CreateLevelRequest>()
            .WithName("CreateLevel")
            .WithSummary("Create a level (admin)")
            .Produces<LevelDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
