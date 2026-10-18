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

namespace Nomax.Modules.Content.Features.Levels;

/// <summary>Only the fields that are present change; <see cref="Version"/> is the one read before editing.</summary>
public sealed record UpdateLevelRequest(uint Version, string? Code = null, TextBlock? Name = null, int? SortOrder = null);

internal sealed class UpdateLevelValidator : AbstractValidator<UpdateLevelRequest>
{
    public UpdateLevelValidator()
    {
        RuleFor(request => request.Code!)
            .Must(ContentRules.IsValidLevelCode)
            .When(request => request.Code is not null)
            .WithMessage("Must be 1–16 characters of a-z, 0-9 or hyphen.");
        RuleFor(request => request.Name).ValidTextBlock(TextBlockUsage.Title).When(request => request.Name is not null);
        RuleFor(request => request.SortOrder).GreaterThanOrEqualTo(0).When(request => request.SortOrder is not null);
    }
}

/// <summary>PATCH /api/content/levels/{id} — admin.</summary>
internal sealed class UpdateLevelHandler(ContentDbContext db)
{
    public async Task<Result<LevelDto>> HandleAsync(Guid id, UpdateLevelRequest request, CancellationToken cancellationToken)
    {
        var level = await db.Levels.FirstOrDefaultAsync(level => level.Id == id, cancellationToken);
        if (level is null)
        {
            return AppError.NotFound();
        }

        if (request.Code is not null && request.Code != level.Code
            && await db.Levels.AnyAsync(other => other.Code == request.Code, cancellationToken))
        {
            return AppError.Conflict("A level with this code already exists.");
        }

        if (db.ExpectVersion(level, request.Version) is { } stale)
        {
            return stale;
        }

        level.Update(request.Code, request.Name, request.SortOrder);
        if (await db.SaveChangesOrConflictAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return LevelDto.From(level, await StructureQueries.LevelBooksAsync(db, level.Id, cancellationToken));
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPatch("/levels/{id:guid}", async (Guid id, UpdateLevelRequest request, UpdateLevelHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.AdminOnly)
            .WithValidation<UpdateLevelRequest>()
            .WithName("UpdateLevel")
            .WithSummary("Change a level (admin)")
            .Produces<LevelDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
