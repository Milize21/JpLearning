using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Classroom.Domain;
using Nomax.Modules.Classroom.Features.Common;
using Nomax.Modules.Classroom.Infrastructure;
using Nomax.SharedKernel.Paging;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Classroom.Features.Classes;

public sealed record ListClassesQuery(
    [property: FromQuery(Name = "status")] string? Status,
    [property: FromQuery(Name = "page")] int? Page,
    [property: FromQuery(Name = "pageSize")] int? PageSize);

/// <summary>
/// GET /api/classroom/classes — admin: all classes; sensei: the classes they teach; murid: active memberships in
/// active or archived classes (docs/05 §6). Filtered and paged in SQL.
/// </summary>
internal sealed class ListClassesHandler(ClassroomDbContext db, ClassAccess access, ClassViews views)
{
    public async Task<Result<PagedResult<ClassDto>>> HandleAsync(ListClassesQuery query, CancellationToken cancellationToken)
    {
        var statusFilter = QueryEnums.Parse<ClassStatus>(query.Status, "status");
        if (!statusFilter.IsSuccess)
        {
            return statusFilter.Error;
        }

        var userId = access.UserId;
        var classes = db.Classes.AsNoTracking();
        if (access.IsStudent)
        {
            classes = classes.Where(item => item.Status != ClassStatus.Draft && db.ClassStudents.Any(member =>
                member.ClassId == item.Id && member.StudentId == userId && member.Status == MemberStatus.Active));
        }
        else if (!access.IsAdmin)
        {
            classes = classes.Where(item => db.ClassTeachers.Any(teacher => teacher.ClassId == item.Id && teacher.TeacherId == userId));
        }

        if (statusFilter.Value is { } status)
        {
            classes = classes.Where(item => item.Status == status);
        }

        var page = PageRequest.Create(query.Page, query.PageSize);
        var total = await classes.CountAsync(cancellationToken);
        var rows = await classes
            .OrderBy(item => item.Status == ClassStatus.Archived)
            .ThenBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ClassDto>(await views.BuildAsync(rows, userId, cancellationToken), page.Page, page.PageSize, total);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/classes", async ([AsParameters] ListClassesQuery query, ListClassesHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToOkResult())
            .WithName("ListClasses")
            .WithSummary("Classes the caller may see: all (admin), taught (sensei), joined (murid)")
            .Produces<PagedResult<ClassDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
}
