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

/// <summary>Students by id (admin pickers) or by username (teachers, who cannot browse other students; docs/01 §4).</summary>
public sealed record AddStudentsRequest(IReadOnlyList<Guid>? StudentIds = null, IReadOnlyList<string>? Usernames = null);

/// <param name="NotFound">Usernames or ids that are not active student accounts; nothing was added for them.</param>
public sealed record AddStudentsResponse(IReadOnlyList<ClassStudentDto> Added, IReadOnlyList<string> NotFound);

internal sealed class AddStudentsValidator : AbstractValidator<AddStudentsRequest>
{
    public AddStudentsValidator()
    {
        RuleFor(request => (request.StudentIds == null ? 0 : request.StudentIds.Count) + (request.Usernames == null ? 0 : request.Usernames.Count))
            .InclusiveBetween(1, 200)
            .OverridePropertyName("studentIds")
            .WithMessage("Send 1–200 students.");
    }
}

/// <summary>GET · POST /api/classroom/classes/{id}/students and DELETE …/{studentId} — teachers of the class and admins.</summary>
internal sealed class StudentsHandler(ClassroomDbContext db, ClassAccess access, IUserDirectory users, TimeProvider timeProvider)
{
    public async Task<Result<IReadOnlyList<ClassStudentDto>>> ListAsync(Guid classId, CancellationToken cancellationToken)
    {
        var found = await access.ForTeacherAsync(classId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var members = await db.ClassStudents.AsNoTracking().Where(member => member.ClassId == classId).ToListAsync(cancellationToken);
        var profiles = await users.GetUsersAsync([.. members.Select(member => member.StudentId)], cancellationToken);
        return members
            .Select(member => ToDto(member, profiles.GetValueOrDefault(member.StudentId)))
            .OrderBy(member => member.Status)
            .ThenBy(member => member.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<Result<AddStudentsResponse>> AddAsync(Guid classId, AddStudentsRequest request, CancellationToken cancellationToken)
    {
        var found = await access.ForTeacherAsync(classId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        if (ClassAccess.EnsureWritable(found.Value) is { } readOnly)
        {
            return readOnly;
        }

        var candidates = new Dictionary<Guid, UserSummary>();
        var notFound = new List<string>();
        if (request.StudentIds is { Count: > 0 } ids)
        {
            var byId = await users.GetUsersAsync(ids, cancellationToken);
            foreach (var id in ids.Distinct())
            {
                if (byId.TryGetValue(id, out var user) && IsActiveStudent(user))
                {
                    candidates[id] = user;
                }
                else
                {
                    notFound.Add(id.ToString());
                }
            }
        }

        if (request.Usernames is { Count: > 0 } usernames)
        {
            var byName = await users.FindByUsernamesAsync(usernames, cancellationToken);
            foreach (var username in usernames.Select(name => name.Trim().ToLowerInvariant()).Where(name => name.Length > 0).Distinct())
            {
                if (byName.TryGetValue(username, out var user) && IsActiveStudent(user))
                {
                    candidates[user.Id] = user;
                }
                else
                {
                    notFound.Add(username);
                }
            }
        }

        var candidateIds = candidates.Keys.ToList();
        var existing = await db.ClassStudents
            .Where(member => member.ClassId == classId && candidateIds.Contains(member.StudentId))
            .ToDictionaryAsync(member => member.StudentId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var added = new List<ClassStudentDto>();
        foreach (var (studentId, user) in candidates)
        {
            if (existing.TryGetValue(studentId, out var member))
            {
                member.Rejoin(now);
            }
            else
            {
                member = ClassStudent.Join(classId, studentId, now);
                db.ClassStudents.Add(member);
            }

            added.Add(ToDto(member, user));
        }

        await db.SaveChangesAsync(cancellationToken);
        return new AddStudentsResponse(added, notFound);
    }

    public async Task<Result> RemoveAsync(Guid classId, Guid studentId, CancellationToken cancellationToken)
    {
        var found = await access.ForTeacherAsync(classId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        if (ClassAccess.EnsureWritable(found.Value) is { } readOnly)
        {
            return readOnly;
        }

        var member = await db.ClassStudents.FirstOrDefaultAsync(
            member => member.ClassId == classId && member.StudentId == studentId && member.Status == MemberStatus.Active,
            cancellationToken);
        if (member is null)
        {
            return AppError.NotFound();
        }

        member.Remove();
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static bool IsActiveStudent(UserSummary user) => user.Role == Roles.Murid && user.IsActive;

    private static ClassStudentDto ToDto(ClassStudent member, UserSummary? user) =>
        new(member.StudentId, user?.Username ?? string.Empty, user?.DisplayName ?? string.Empty, member.Status, member.JoinedAt, user?.IsActive ?? false);

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/classes/{id:guid}/students", async (Guid id, StudentsHandler handler, CancellationToken cancellationToken) =>
                (await handler.ListAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("ListClassStudents")
            .WithSummary("Students of a class, including removed ones (teachers, admin)")
            .Produces<IReadOnlyList<ClassStudentDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/classes/{id:guid}/students", async (Guid id, AddStudentsRequest request, StudentsHandler handler, CancellationToken cancellationToken) =>
                (await handler.AddAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<AddStudentsRequest>()
            .WithName("AddClassStudents")
            .WithSummary("Add active student accounts by id or username (teachers, admin)")
            .Produces<AddStudentsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/classes/{id:guid}/students/{studentId:guid}", async (Guid id, Guid studentId, StudentsHandler handler, CancellationToken cancellationToken) =>
                (await handler.RemoveAsync(id, studentId, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("RemoveClassStudent")
            .WithSummary("Mark a student as removed from the class (teachers, admin)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
