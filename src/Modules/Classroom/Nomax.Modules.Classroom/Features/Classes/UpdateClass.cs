using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Classroom.Domain;
using Nomax.Modules.Classroom.Features.Common;
using Nomax.Modules.Classroom.Infrastructure;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Classroom.Features.Classes;

/// <param name="Status">Switch between <c>draft</c> and <c>active</c>; archiving has its own endpoint.</param>
public sealed record UpdateClassRequest(string? Name = null, ClassStatus? Status = null);

internal sealed class UpdateClassValidator : AbstractValidator<UpdateClassRequest>
{
    public UpdateClassValidator()
    {
        RuleFor(request => request.Name).Must(name => name is null || name.Trim().Length is > 0 and <= StudyClass.NameMaxLength)
            .WithMessage($"Must be 1–{StudyClass.NameMaxLength} characters.");
        RuleFor(request => request.Status).Must(status => status is null or ClassStatus.Draft or ClassStatus.Active)
            .WithMessage("Must be draft or active.");
    }
}

/// <summary>PATCH /api/classroom/classes/{id} — teachers of the class and admins.</summary>
internal sealed class UpdateClassHandler(ClassroomDbContext db, ClassAccess access, ClassViews views)
{
    public async Task<Result<ClassDto>> HandleAsync(Guid id, UpdateClassRequest request, CancellationToken cancellationToken)
    {
        var found = await access.ForTeacherAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var item = found.Value;
        if (ClassAccess.EnsureWritable(item) is { } readOnly)
        {
            return readOnly;
        }

        if (request.Name is not null)
        {
            item.Rename(request.Name);
        }

        if (request.Status is { } status && !item.TrySetStatus(status))
        {
            return AppError.InvalidState("The status change is not allowed.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return await views.BuildAsync(item, access.UserId, cancellationToken);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPatch("/classes/{id:guid}", async (Guid id, UpdateClassRequest request, UpdateClassHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<UpdateClassRequest>()
            .WithName("UpdateClass")
            .WithSummary("Rename a class or switch it between draft and active (teachers, admin)")
            .Produces<ClassDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
