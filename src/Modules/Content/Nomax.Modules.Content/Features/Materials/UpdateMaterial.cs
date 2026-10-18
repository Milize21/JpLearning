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
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel;
using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Features.Materials;

/// <summary>
/// Only the fields that are present change. <see cref="ClearPublishAt"/> publishes at once. The content field must match the
/// material's kind; replacing the file of a file material marks the old file deleted.
/// </summary>
public sealed record UpdateMaterialRequest(
    uint Version,
    TextBlock? Title = null,
    RichText? Body = null,
    Guid? FileId = null,
    string? Url = null,
    DateTimeOffset? PublishAt = null,
    bool ClearPublishAt = false,
    int? SortOrder = null);

internal sealed class UpdateMaterialValidator : AbstractValidator<UpdateMaterialRequest>
{
    public UpdateMaterialValidator()
    {
        RuleFor(request => request.Title).ValidTextBlock(TextBlockUsage.Title).When(request => request.Title is not null);
        RuleFor(request => request.Body).ValidRichText().When(request => request.Body is not null);
        RuleFor(request => request.Url).HttpsUrl(Material.UrlMaxLength).When(request => request.Url is not null);
        RuleFor(request => request.SortOrder).GreaterThanOrEqualTo(0).When(request => request.SortOrder is not null);
    }
}

/// <summary>PATCH /api/content/materials/{id} — the creator or an admin (BR-KONTEN-02); other staff get 403.</summary>
internal sealed class UpdateMaterialHandler(ContentDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<MaterialDto>> HandleAsync(Guid id, UpdateMaterialRequest request, CancellationToken cancellationToken)
    {
        var material = await db.Materials.FirstOrDefaultAsync(material => material.Id == id, cancellationToken);
        if (material is null)
        {
            return AppError.NotFound();
        }

        var isAdmin = currentUser.IsInRole(Roles.Admin);
        if (!isAdmin && material.CreatedBy != currentUser.Id)
        {
            return AppError.Forbidden("Only the creator or an admin changes a material.");
        }

        if (db.ExpectVersion(material, request.Version) is { } stale)
        {
            return stale;
        }

        StoredFile? newFile = null;
        if (request.FileId is { } fileId && fileId != material.FileId)
        {
            var checkedFile = await MaterialFiles.CheckAsync(db, fileId, currentUser.Id, isAdmin, cancellationToken);
            if (!checkedFile.IsSuccess)
            {
                return checkedFile.Error;
            }

            newFile = checkedFile.Value;
        }

        var oldFileId = material.FileId;
        if (!material.TryChangeContent(request.Body, request.FileId, request.Url))
        {
            return AppError.Validation(new Dictionary<string, string[]> { ["kind"] = ["Send only the content field of the material's kind."] });
        }

        material.Update(request.Title, request.SortOrder);
        if (request.ClearPublishAt)
        {
            material.ChangePublishAt(null);
        }
        else if (request.PublishAt is not null)
        {
            material.ChangePublishAt(request.PublishAt);
        }

        if (newFile is not null && oldFileId is { } replaced)
        {
            var old = await db.StoredFiles.FirstOrDefaultAsync(file => file.Id == replaced, cancellationToken);
            old?.MarkDeleted();
        }

        if (await db.SaveChangesOrConflictAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        var file = material.FileId is { } currentFileId
            ? await db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(file => file.Id == currentFileId, cancellationToken)
            : null;
        return MaterialDto.From(material, file);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPatch("/materials/{id:guid}", async (Guid id, UpdateMaterialRequest request, UpdateMaterialHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<UpdateMaterialRequest>()
            .WithName("UpdateMaterial")
            .WithSummary("Change a material (creator, admin)")
            .Produces<MaterialDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
