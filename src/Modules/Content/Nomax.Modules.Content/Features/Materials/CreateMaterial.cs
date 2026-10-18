using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Content.Domain;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Features.Materials;

/// <summary>
/// One of <see cref="Body"/> (richtext), <see cref="FileId"/> (file, a finished upload) or <see cref="Url"/> (link, https only),
/// matching <see cref="Kind"/>. <see cref="PublishAt"/> in the future hides the material from students until then.
/// </summary>
public sealed record CreateMaterialRequest(
    MaterialKind Kind,
    TextBlock Title,
    RichText? Body = null,
    Guid? FileId = null,
    string? Url = null,
    DateTimeOffset? PublishAt = null,
    int SortOrder = 0);

internal sealed class CreateMaterialValidator : AbstractValidator<CreateMaterialRequest>
{
    public CreateMaterialValidator()
    {
        RuleFor(request => request.Kind).IsInEnum();
        RuleFor(request => request.Title).ValidTextBlock(TextBlockUsage.Title);
        RuleFor(request => request.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(request => request.Body).ValidRichText().When(request => request.Kind == MaterialKind.Richtext);
        RuleFor(request => request.FileId).NotNull().When(request => request.Kind == MaterialKind.File);
        RuleFor(request => request.Url).HttpsUrl(Material.UrlMaxLength).When(request => request.Kind == MaterialKind.Link);
        RuleFor(request => request)
            .Must(request => request.Kind switch
            {
                MaterialKind.Richtext => request.FileId is null && request.Url is null,
                MaterialKind.File => request.Body is null && request.Url is null,
                _ => request.Body is null && request.FileId is null,
            })
            .OverridePropertyName("kind")
            .WithMessage("Send only the content field of the chosen kind.");
    }
}

/// <summary>POST /api/content/chapters/{id}/materials — staff.</summary>
internal sealed class CreateMaterialHandler(ContentDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<Result<MaterialDto>> HandleAsync(Guid chapterId, CreateMaterialRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Chapters.AnyAsync(chapter => chapter.Id == chapterId, cancellationToken))
        {
            return AppError.NotFound();
        }

        var id = Uuid7.New(timeProvider);
        StoredFile? file = null;
        Material material;
        switch (request.Kind)
        {
            case MaterialKind.File:
                var checkedFile = await MaterialFiles.CheckAsync(db, request.FileId!.Value, currentUser.Id, currentUser.IsInRole(Roles.Admin), cancellationToken);
                if (!checkedFile.IsSuccess)
                {
                    return checkedFile.Error;
                }

                file = checkedFile.Value;
                material = Material.CreateFile(id, chapterId, request.Title, file.Id, request.PublishAt, currentUser.Id, request.SortOrder);
                break;
            case MaterialKind.Link:
                material = Material.CreateLink(id, chapterId, request.Title, request.Url!, request.PublishAt, currentUser.Id, request.SortOrder);
                break;
            default:
                material = Material.CreateRichText(id, chapterId, request.Title, request.Body!, request.PublishAt, currentUser.Id, request.SortOrder);
                break;
        }

        db.Materials.Add(material);
        await db.SaveChangesAsync(cancellationToken);
        return MaterialDto.From(material, file);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/chapters/{id:guid}/materials", async (Guid id, CreateMaterialRequest request, CreateMaterialHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken))
                    .ToHttpResult(material => TypedResults.Created($"/api/content/materials/{material.Id}", material)))
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<CreateMaterialRequest>()
            .WithName("CreateMaterial")
            .WithSummary("Add a reading text, file or https link to a chapter (staff)")
            .Produces<MaterialDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
