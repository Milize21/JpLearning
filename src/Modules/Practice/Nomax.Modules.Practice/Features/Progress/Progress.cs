using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Identity.Contracts;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Common;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.SharedKernel.Paging;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Practice.Features.Progress;

/// <param name="TopWeakness">The weakest group of BR-MASTERY-03, if any.</param>
public sealed record StudentProgressDto(
    Guid StudentId,
    string DisplayName,
    DateTimeOffset? LastPracticedAt,
    int AnswersLast7Days,
    decimal? Accuracy,
    decimal? AverageMastery,
    int WeakItemCount,
    WeaknessGroupDto? TopWeakness);

public sealed record ClassProgressDto(Guid ClassId, Guid LevelId, IReadOnlyList<StudentProgressDto> Students);

/// <summary>
/// Progress and weaknesses (BR-MASTERY-02/03): /me/* for the student, /students/{id}/* for <c>SenseiDari</c> and admins,
/// /classes/{id}/progress for the class's teachers and admins. Other callers get 404 (docs/01 §4).
/// </summary>
internal sealed class ProgressHandler(
    PracticeDbContext db,
    PracticeAccess access,
    MasteryViews views,
    IClassAccessReader classes,
    IUserDirectory users,
    TimeProvider timeProvider)
{
    public async Task<PagedResult<DrillSessionSummaryDto>> MySessionsAsync(int? page, int? pageSize, CancellationToken cancellationToken) =>
        await SessionsAsync(access.UserId, page, pageSize, cancellationToken);

    public async Task<Result<IReadOnlyList<MasteryItemDto>>> MyMasteryAsync(Guid? chapterId, string? category, CancellationToken cancellationToken)
    {
        var parsed = QueryEnums.Parse<Category>(category, "category");
        if (!parsed.IsSuccess)
        {
            return parsed.Error;
        }

        var rows = await views.RowsAsync(access.UserId, null, chapterId, parsed.Value, cancellationToken);
        return Result.Success(await views.ToDtosAsync(rows, cancellationToken));
    }

    public async Task<WeaknessesDto> MyWeaknessesAsync(CancellationToken cancellationToken) =>
        await views.WeaknessesAsync(await views.RowsAsync(access.UserId, null, null, null, cancellationToken), cancellationToken);

    public async Task<Result<WeaknessesDto>> StudentWeaknessesAsync(Guid studentId, Guid? classId, CancellationToken cancellationToken)
    {
        if (!await access.CanSeeStudentAsync(studentId, cancellationToken) || access.IsStudent)
        {
            return AppError.NotFound();
        }

        Guid? levelId = null;
        if (classId is { } id)
        {
            var info = await classes.GetClassInfoAsync(id, cancellationToken);
            if (info is null || !await access.CanTeachClassAsync(id, cancellationToken) || !await classes.IsStudentOfAsync(studentId, id, cancellationToken))
            {
                return AppError.NotFound();
            }

            levelId = info.LevelId;
        }

        return await views.WeaknessesAsync(await views.RowsAsync(studentId, levelId, null, null, cancellationToken), cancellationToken);
    }

    public async Task<Result<PagedResult<DrillSessionSummaryDto>>> StudentSessionsAsync(Guid studentId, int? page, int? pageSize, CancellationToken cancellationToken) =>
        !access.IsStudent && await access.CanSeeStudentAsync(studentId, cancellationToken)
            ? await SessionsAsync(studentId, page, pageSize, cancellationToken)
            : AppError.NotFound();

    public async Task<Result<ClassProgressDto>> ClassProgressAsync(Guid classId, CancellationToken cancellationToken)
    {
        var info = await classes.GetClassInfoAsync(classId, cancellationToken);
        if (info is null || !await access.CanTeachClassAsync(classId, cancellationToken))
        {
            return AppError.NotFound();
        }

        var studentIds = (await classes.GetClassStudentIdsAsync(classId, cancellationToken)).ToList();
        var profiles = await users.GetUsersAsync(studentIds, cancellationToken);
        var since = timeProvider.GetUtcNow().AddDays(-7);

        var activity = await db.DrillAnswers.AsNoTracking()
            .Join(db.DrillSessions.AsNoTracking(), answer => answer.SessionId, session => session.Id, (answer, session) => new { session.StudentId, answer.CreatedAt })
            .Where(row => studentIds.Contains(row.StudentId))
            .GroupBy(row => row.StudentId)
            .Select(group => new { StudentId = group.Key, Last = group.Max(row => row.CreatedAt), Recent = group.Count(row => row.CreatedAt >= since) })
            .ToDictionaryAsync(row => row.StudentId, cancellationToken);

        var rows = (await db.ItemMasteries.AsNoTracking()
                .Where(mastery => studentIds.Contains(mastery.StudentId) && mastery.LevelId == info.LevelId && mastery.Attempts >= 1)
                .ToListAsync(cancellationToken))
            .ToLookup(mastery => mastery.StudentId);

        var settings = views.Settings;
        var students = studentIds
            .Select(studentId =>
            {
                var mine = rows[studentId].ToList();
                var attempts = mine.Sum(row => row.Attempts);
                var report = WeaknessAggregation.Aggregate(mine.Select(MasteryViews.ToRow), settings, topGroups: 1, weakestItems: 0);
                var top = report.Groups.Count > 0 ? report.Groups[0] : null;
                activity.TryGetValue(studentId, out var stats);
                return new StudentProgressDto(
                    studentId,
                    profiles.TryGetValue(studentId, out var profile) ? profile.DisplayName : string.Empty,
                    stats?.Last,
                    stats?.Recent ?? 0,
                    attempts == 0 ? null : Math.Round((decimal)mine.Sum(row => row.Correct) / attempts, 3, MidpointRounding.AwayFromZero),
                    mine.Count == 0 ? null : Math.Round(mine.Average(row => row.Mastery), 3, MidpointRounding.AwayFromZero),
                    mine.Count(row => MasteryRules.StatusOf(row.Attempts, row.Mastery, row.Streak, settings) == MasteryStatus.Weak),
                    top is null ? null : new WeaknessGroupDto(top.Kind, top.Key, top.ItemCount, top.AverageMastery, top.Accuracy, top.WeakCount));
            })
            .OrderBy(student => student.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new ClassProgressDto(classId, info.LevelId, students);
    }

    private async Task<PagedResult<DrillSessionSummaryDto>> SessionsAsync(Guid studentId, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var request = PageRequest.Create(page, pageSize);
        var sessions = db.DrillSessions.AsNoTracking().Where(session => session.StudentId == studentId);
        var total = await sessions.CountAsync(cancellationToken);
        var rows = await sessions
            .OrderByDescending(session => session.StartedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(session => new
            {
                Session = session,
                Answers = db.DrillAnswers.Count(answer => answer.SessionId == session.Id),
                Correct = db.DrillAnswers.Count(answer => answer.SessionId == session.Id && answer.IsCorrect),
            })
            .ToListAsync(cancellationToken);
        return new PagedResult<DrillSessionSummaryDto>(
            [.. rows.Select(row => new DrillSessionSummaryDto(
                row.Session.Id, row.Session.Category, row.Session.Config.Mode, row.Session.ClassId, row.Session.StartedAt, row.Session.EndedAt, row.Answers, row.Correct))],
            request.Page,
            request.PageSize,
            total);
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/me/drill-sessions", async ([FromQuery(Name = "page")] int? page, [FromQuery(Name = "pageSize")] int? pageSize, ProgressHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.MySessionsAsync(page, pageSize, cancellationToken)))
            .RequireAuthorization(Policies.MuridOnly)
            .WithName("ListMyDrillSessions")
            .WithSummary("My drill sessions, newest first (murid)")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/me/mastery", async ([FromQuery(Name = "chapterId")] Guid? chapterId, [FromQuery(Name = "category")] string? category, ProgressHandler handler, CancellationToken cancellationToken) =>
                (await handler.MyMasteryAsync(chapterId, category, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.MuridOnly)
            .WithName("GetMyMastery")
            .WithSummary("My items with mastery and status (murid)")
            .Produces<IReadOnlyList<MasteryItemDto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/me/weaknesses", async (ProgressHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.MyWeaknessesAsync(cancellationToken)))
            .RequireAuthorization(Policies.MuridOnly)
            .WithName("GetMyWeaknesses")
            .WithSummary("My weakest groups and items (murid)")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/students/{id:guid}/weaknesses", async (Guid id, [FromQuery(Name = "classId")] Guid? classId, ProgressHandler handler, CancellationToken cancellationToken) =>
                (await handler.StudentWeaknessesAsync(id, classId, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("GetStudentWeaknesses")
            .WithSummary("A student's weaknesses, optionally within a class's level (teachers of the student, admin)")
            .Produces<WeaknessesDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/students/{id:guid}/drill-sessions", async (Guid id, [FromQuery(Name = "page")] int? page, [FromQuery(Name = "pageSize")] int? pageSize, ProgressHandler handler, CancellationToken cancellationToken) =>
                (await handler.StudentSessionsAsync(id, page, pageSize, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("ListStudentDrillSessions")
            .WithSummary("A student's drill sessions (teachers of the student, admin)")
            .Produces<PagedResult<DrillSessionSummaryDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/classes/{id:guid}/progress", async (Guid id, ProgressHandler handler, CancellationToken cancellationToken) =>
                (await handler.ClassProgressAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("GetClassProgress")
            .WithSummary("Per student: last practice, answers in 7 days, accuracy, mastery, top weakness (teachers, admin)")
            .Produces<ClassProgressDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
