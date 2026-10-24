using System.Buffers.Text;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Identity.Contracts;
using Nomax.Modules.Insight.Domain;
using Nomax.Modules.Insight.Infrastructure;
using Nomax.Modules.Insight.IntegrationEvents;
using Nomax.SharedKernel;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Insight.Features;

/// <param name="Type">docs/05 §11, e.g. <c>drill_session_ended</c>.</param>
public sealed record ActivityDto(Guid Id, string Type, string? Ref, JsonElement Meta, DateTimeOffset Ts);

/// <param name="NextCursor">Pass as <c>?cursor=</c> for older entries; null at the end.</param>
public sealed record ActivityPage(IReadOnlyList<ActivityDto> Items, string? NextCursor);

/// <param name="LastActiveAt">Last drilling session, graded quiz or point (D-41); null before any activity.</param>
public sealed record ActivitySummaryDto(Guid StudentId, string DisplayName, DateTimeOffset? LastActiveAt);

/// <summary>
/// Activity log (docs/05 §7a): GET /me/activity for everyone, GET /students/{id}/activity for the student's teachers
/// (<c>SenseiDari</c>) and admins, GET /classes/{id}/activity-summary ("terakhir aktif") for the class's teachers and admins.
/// </summary>
internal sealed class ActivityHandler(InsightDbContext db, ICurrentUser currentUser, IClassAccessReader classes, IUserDirectory users)
{
    private const int PageSize = 30;
    private static readonly string[] CursorMessage = ["The cursor is not valid."];

    // Students see their own data only (docs/01 §4): which staff account graded them or created their account stays with staff.
    private static readonly string[] StaffIdKeys = ["gradedBy", "createdBy"];

    private bool IsAdmin => currentUser.IsInRole(Roles.Admin);

    public Task<Result<ActivityPage>> MineAsync(string? cursor, CancellationToken cancellationToken) =>
        PageAsync(currentUser.Id, cursor, hideStaffIds: currentUser.IsInRole(Roles.Murid), cancellationToken);

    public async Task<Result<ActivityPage>> StudentAsync(Guid studentId, string? cursor, CancellationToken cancellationToken)
    {
        var visible = IsAdmin || await classes.CanTeacherSeeStudentAsync(currentUser.Id, studentId, cancellationToken);
        return visible ? await PageAsync(studentId, cursor, hideStaffIds: false, cancellationToken) : AppError.NotFound();
    }

    public async Task<Result<IReadOnlyList<ActivitySummaryDto>>> ClassSummaryAsync(Guid classId, CancellationToken cancellationToken)
    {
        var info = await classes.GetClassInfoAsync(classId, cancellationToken);
        if (info is null || !(IsAdmin || await classes.IsTeacherOfAsync(currentUser.Id, classId, cancellationToken)))
        {
            return AppError.NotFound();
        }

        var students = await classes.GetClassStudentIdsAsync(classId, cancellationToken);
        if (students.Count == 0)
        {
            return Result.Success<IReadOnlyList<ActivitySummaryDto>>([]);
        }

        var ids = students.ToList();
        var fromLogs = await db.ActivityLogs.AsNoTracking()
            .Where(log => ids.Contains(log.UserId) && ActivityTypes.StudentActivity.Contains(log.Type))
            .GroupBy(log => log.UserId)
            .Select(group => new { UserId = group.Key, Last = group.Max(log => log.Ts) })
            .ToDictionaryAsync(row => row.UserId, row => row.Last, cancellationToken);
        var fromPoints = await db.PointEvents.AsNoTracking()
            .Where(item => ids.Contains(item.StudentId))
            .GroupBy(item => item.StudentId)
            .Select(group => new { UserId = group.Key, Last = group.Max(item => item.Ts) })
            .ToDictionaryAsync(row => row.UserId, row => row.Last, cancellationToken);
        var profiles = await users.GetUsersAsync(ids, cancellationToken);

        return Result.Success<IReadOnlyList<ActivitySummaryDto>>(
        [
            .. ids.Select(id => new ActivitySummaryDto(
                    id,
                    profiles.TryGetValue(id, out var profile) ? profile.DisplayName : string.Empty,
                    Latest(fromLogs.GetValueOrDefault(id), fromPoints.GetValueOrDefault(id))))
                .OrderBy(row => row.DisplayName, StringComparer.CurrentCultureIgnoreCase),
        ]);
    }

    private async Task<Result<ActivityPage>> PageAsync(Guid userId, string? cursor, bool hideStaffIds, CancellationToken cancellationToken)
    {
        var query = db.ActivityLogs.AsNoTracking().Where(log => log.UserId == userId);
        if (!string.IsNullOrEmpty(cursor))
        {
            if (!TryReadCursor(cursor, out var ts, out var id))
            {
                return AppError.Validation(new Dictionary<string, string[]> { ["cursor"] = CursorMessage });
            }

            query = query.Where(log => log.Ts < ts || (log.Ts == ts && log.Id.CompareTo(id) < 0));
        }

        var rows = await query.OrderByDescending(log => log.Ts).ThenByDescending(log => log.Id).Take(PageSize + 1).ToListAsync(cancellationToken);
        var items = rows.Take(PageSize)
            .Select(log => new ActivityDto(log.Id, log.Type, log.Ref, hideStaffIds ? WithoutStaffIds(log.Meta) : log.Meta, log.Ts))
            .ToList();
        var next = rows.Count > PageSize ? WriteCursor(items[^1].Ts, items[^1].Id) : null;
        return new ActivityPage(items, next);
    }

    private static JsonElement WithoutStaffIds(JsonElement meta)
    {
        if (meta.ValueKind != JsonValueKind.Object || !StaffIdKeys.Any(key => meta.TryGetProperty(key, out _)))
        {
            return meta;
        }

        var copy = JsonObject.Create(meta)!;
        foreach (var key in StaffIdKeys)
        {
            copy.Remove(key);
        }

        return JsonSerializer.SerializeToElement(copy);
    }

    private static DateTimeOffset? Latest(DateTimeOffset a, DateTimeOffset b)
    {
        var latest = a > b ? a : b;
        return latest == default ? null : latest;
    }

    private static string WriteCursor(DateTimeOffset ts, Guid id) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{ts.UtcTicks}:{id}")));

    private static bool TryReadCursor(string cursor, out DateTimeOffset ts, out Guid id)
    {
        ts = default;
        id = default;
        try
        {
            var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor)).Split(':');
            if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                || ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks || !Guid.TryParse(parts[1], out id))
            {
                return false;
            }

            ts = new DateTimeOffset(ticks, TimeSpan.Zero);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/me/activity", async (string? cursor, ActivityHandler handler, CancellationToken cancellationToken) =>
                (await handler.MineAsync(cursor, cancellationToken)).ToOkResult())
            .WithName("GetMyActivity")
            .WithSummary("My activity log, newest first")
            .Produces<ActivityPage>()
            .ProducesValidationProblem();

        group.MapGet("/students/{id:guid}/activity", async (Guid id, string? cursor, ActivityHandler handler, CancellationToken cancellationToken) =>
                (await handler.StudentAsync(id, cursor, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("GetStudentActivity")
            .WithSummary("Activity log of a student (their teachers, admin)")
            .Produces<ActivityPage>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/classes/{id:guid}/activity-summary", async (Guid id, ActivityHandler handler, CancellationToken cancellationToken) =>
                (await handler.ClassSummaryAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("GetClassActivitySummary")
            .WithSummary("Last activity per student of a class (teachers, admin)")
            .Produces<IReadOnlyList<ActivitySummaryDto>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
