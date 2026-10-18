using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Content.Features.Materials;

/// <summary>
/// DELETE /api/content/materials/{id} — the creator or an admin (BR-KONTEN-02). The file of a file material is marked
/// deleted; the cleanup job removes the bytes.
/// </summary>
internal sealed class DeleteMaterialHandler(ContentDbContext db, ICurrentUser currentUser)
{
    public async Task<Result> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var material = await db.Materials.FirstOrDefaultAsync(material => material.Id == id, cancellationToken);
        if (material is null)
        {
            return AppError.NotFound();
        }

        if (!currentUser.IsInRole(Roles.Admin) && material.CreatedBy != currentUser.Id)
        {
            return AppError.Forbidden("Only the creator or an admin deletes a material.");
        }

        if (material.FileId is { } fileId)
        {
            var file = await db.StoredFiles.FirstOrDefaultAsync(file => file.Id == fileId, cancellationToken);
            file?.MarkDeleted();
        }

        db.Materials.Remove(material);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapDelete("/materials/{id:guid}", async (Guid id, DeleteMaterialHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("DeleteMaterial")
            .WithSummary("Delete a material (creator, admin)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
