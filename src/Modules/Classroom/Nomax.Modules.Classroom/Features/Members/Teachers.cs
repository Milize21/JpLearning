using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Classroom.Domain;
using Nomax.Modules.Classroom.Features.Common;
using Nomax.Modules.Classroom.Infrastructure;
using Nomax.Modules.Identity.Contracts;
using Nomax.SharedKernel;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Classroom.Features.Members;

/// <summary>A sensei or admin account by id or by username.</summary>
public sealed record AddTeacherRequest(Guid? TeacherId = null, string? Username = null, bool IsPrimary = false);

internal sealed class AddTeacherValidator : AbstractValidator<AddTeacherRequest>
{
    public AddTeacherValidator()
    {
        RuleFor(request => request).Must(request => request.TeacherId is not null || !string.IsNullOrWhiteSpace(request.Username))
            .OverridePropertyName("teacherId")
            .WithMessage("Send teacherId or username.");
    }
}

/// <summary>
/// GET · POST /api/classroom/classes/{id}/teachers and DELETE …/{teacherId} — primary teacher or admin (BR-AKSES-03).
/// The last teacher cannot be removed; when the primary teacher leaves, the earliest other teacher becomes primary.
/// </summary>
internal sealed class TeachersHandler(ClassroomDbContext db, ClassAccess access, IUserDirectory users)
{
    public async Task<Result<IReadOnlyList<TeacherDto>>> ListAsync(Guid classId, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(classId, cancellationToken) is { } denied)
        {
            return denied;
        }

        return Result.Success(await LoadAsync(classId, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<TeacherDto>>> AddAsync(Guid classId, AddTeacherRequest request, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(classId, cancellationToken) is { } denied)
        {
            return denied;
        }

        UserSummary? user = null;
        if (request.TeacherId is { } teacherId)
        {
            user = (await users.GetUsersAsync([teacherId], cancellationToken)).GetValueOrDefault(teacherId);
        }
        else if (request.Username is { } username)
        {
            user = (await users.FindByUsernamesAsync([username], cancellationToken)).GetValueOrDefault(username.Trim().ToLowerInvariant());
        }

        if (user is null || !user.IsActive || user.Role is not (Roles.Sensei or Roles.Admin))
        {
            return AppError.Validation(new Dictionary<string, string[]> { ["teacherId"] = ["Must be an active sensei or admin account."] });
        }

        var teachers = await db.ClassTeachers.Where(teacher => teacher.ClassId == classId).ToListAsync(cancellationToken);
        if (teachers.All(teacher => teacher.TeacherId != user.Id))
        {
            db.ClassTeachers.Add(ClassTeacher.Create(classId, user.Id, isPrimary: false));
            await db.SaveChangesAsync(cancellationToken);
        }

        if (request.IsPrimary)
        {
            // Two steps so the unique "one primary per class" index is never violated in between.
            foreach (var teacher in teachers.Where(teacher => teacher.IsPrimary && teacher.TeacherId != user.Id))
            {
                teacher.SetPrimary(false);
            }

            await db.SaveChangesAsync(cancellationToken);
            var promoted = await db.ClassTeachers.FirstAsync(teacher => teacher.ClassId == classId && teacher.TeacherId == user.Id, cancellationToken);
            promoted.SetPrimary(true);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(await LoadAsync(classId, cancellationToken));
    }

    public async Task<Result> RemoveAsync(Guid classId, Guid teacherId, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(classId, cancellationToken) is { } denied)
        {
            return denied;
        }

        var teachers = await db.ClassTeachers.Where(teacher => teacher.ClassId == classId).ToListAsync(cancellationToken);
        var leaving = teachers.FirstOrDefault(teacher => teacher.TeacherId == teacherId);
        if (leaving is null)
        {
            return AppError.NotFound();
        }

        if (!TeacherRules.CanRemove(teachers.Count))
        {
            return AppError.InvalidState("The last teacher of a class cannot be removed.");
        }

        db.ClassTeachers.Remove(leaving);
        await db.SaveChangesAsync(cancellationToken);
        if (leaving.IsPrimary)
        {
            var next = TeacherRules.NextPrimary(teachers.Where(teacher => teacher != leaving).Select(teacher => (teacher.TeacherId, teacher.CreatedAt)));
            teachers.First(teacher => teacher.TeacherId == next).SetPrimary(true);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    private async Task<AppError?> AuthorizeAsync(Guid classId, CancellationToken cancellationToken)
    {
        var found = await access.ForTeacherAsync(classId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        if (!TeacherRules.CanManageTeachers(access.IsAdmin, await access.IsPrimaryTeacherAsync(classId, cancellationToken)))
        {
            return AppError.Forbidden("Only the primary teacher or an admin manages the teachers of a class.");
        }

        return ClassAccess.EnsureWritable(found.Value);
    }

    private async Task<IReadOnlyList<TeacherDto>> LoadAsync(Guid classId, CancellationToken cancellationToken)
    {
        var teachers = await db.ClassTeachers.AsNoTracking()
            .Where(teacher => teacher.ClassId == classId)
            .OrderByDescending(teacher => teacher.IsPrimary)
            .ThenBy(teacher => teacher.CreatedAt)
            .ToListAsync(cancellationToken);
        var names = await users.GetUsersAsync([.. teachers.Select(teacher => teacher.TeacherId)], cancellationToken);
        return [.. teachers.Select(teacher => new TeacherDto(teacher.TeacherId, names.GetValueOrDefault(teacher.TeacherId)?.DisplayName ?? string.Empty, teacher.IsPrimary))];
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/classes/{id:guid}/teachers", async (Guid id, TeachersHandler handler, CancellationToken cancellationToken) =>
                (await handler.ListAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("ListClassTeachers")
            .WithSummary("Teachers of a class (primary teacher, admin)")
            .Produces<IReadOnlyList<TeacherDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/classes/{id:guid}/teachers", async (Guid id, AddTeacherRequest request, TeachersHandler handler, CancellationToken cancellationToken) =>
                (await handler.AddAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<AddTeacherRequest>()
            .WithName("AddClassTeacher")
            .WithSummary("Add a teacher, optionally as the new primary teacher (primary teacher, admin)")
            .Produces<IReadOnlyList<TeacherDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/classes/{id:guid}/teachers/{teacherId:guid}", async (Guid id, Guid teacherId, TeachersHandler handler, CancellationToken cancellationToken) =>
                (await handler.RemoveAsync(id, teacherId, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("RemoveClassTeacher")
            .WithSummary("Remove a teacher; never the last one (primary teacher, admin)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
