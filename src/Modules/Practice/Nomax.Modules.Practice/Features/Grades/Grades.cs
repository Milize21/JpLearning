using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Identity.Contracts;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Common;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Practice.Features.Grades;

/// <param name="Version">0 while the class still uses the defaults (no row yet).</param>
public sealed record GradeSchemeDto(Guid ClassId, decimal WQuiz, decimal WExam, decimal WAssignment, IReadOnlyList<ScaleStep> ScaleMap, bool IsDefault, uint Version);

public sealed record SaveGradeSchemeRequest(decimal WQuiz, decimal WExam, decimal WAssignment, IReadOnlyList<ScaleStep> ScaleMap, uint Version = 0);

public sealed record ClassGradeDto(Guid ClassId, string ClassName, Guid StudentId, string StudentName, decimal? FinalPct, decimal? Gpa, GradeBreakdown Breakdown, DateTimeOffset ComputedAt);

/// <summary>
/// Class grades (docs/05 §7, BR-NILAI-01…06): GET · PUT /classes/{id}/grade-scheme and GET /classes/{id}/grades for teachers
/// and admins; GET /me/grades for students. Grades are recomputed when read, so they reflect closed deadlines at once.
/// </summary>
internal sealed class GradesHandler(
    PracticeDbContext db, PracticeAccess access, ClassScope scope, IClassAccessReader classes, IUserDirectory users, GradeService grades)
{
    private static readonly string[] WeightMessage = ["Weights must be at least 0 and add up to 100."];
    private static readonly string[] MinMessage = ["Minimums must be unique, within 0–100 and include 0."];
    private static readonly string[] GpaMessage = ["Grades must be within 0–4."];

    public async Task<Result<GradeSchemeDto>> SchemeAsync(Guid classId, CancellationToken cancellationToken)
    {
        var found = await scope.ForTeacherAsync(classId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var scheme = await db.GradeSchemes.AsNoTracking().FirstOrDefaultAsync(item => item.ClassId == classId, cancellationToken);
        return scheme is null
            ? new GradeSchemeDto(classId, GradeSettings.Default.WQuiz, GradeSettings.Default.WExam, GradeSettings.Default.WAssignment, GradeSettings.DefaultScale, true, 0)
            : new GradeSchemeDto(classId, scheme.WQuiz, scheme.WExam, scheme.WAssignment, scheme.ScaleMap, false, scheme.Version);
    }

    /// <summary>BR-NILAI-06: validated, saved, and every grade of the class recomputed.</summary>
    public async Task<Result<GradeSchemeDto>> SaveSchemeAsync(Guid classId, SaveGradeSchemeRequest request, CancellationToken cancellationToken)
    {
        var found = await scope.ForTeacherAsync(classId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        if (ClassScope.EnsureWritable(found.Value) is { } readOnly)
        {
            return readOnly;
        }

        var scale = request.ScaleMap ?? [];
        var problems = GradeCalculator.Validate(request.WQuiz, request.WExam, request.WAssignment, scale);
        if (problems.Count > 0)
        {
            return AppError.Validation(problems.ToDictionary(problem => problem, problem => problem switch
            {
                "weights" => WeightMessage,
                "scaleMap.min" => MinMessage,
                _ => GpaMessage,
            }));
        }

        var ordered = scale.OrderByDescending(step => step.Min).ToList();
        var scheme = await db.GradeSchemes.FirstOrDefaultAsync(item => item.ClassId == classId, cancellationToken);
        if (scheme is null)
        {
            db.GradeSchemes.Add(GradeScheme.Create(classId, request.WQuiz, request.WExam, request.WAssignment, ordered));
        }
        else
        {
            if (db.ExpectVersion(scheme, request.Version) is { } stale)
            {
                return stale;
            }

            scheme.Update(request.WQuiz, request.WExam, request.WAssignment, ordered);
        }

        if (await db.SaveChangesOrConflictAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        await grades.RecomputeAsync(classId, null, cancellationToken);
        return await SchemeAsync(classId, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<ClassGradeDto>>> ClassGradesAsync(Guid classId, CancellationToken cancellationToken)
    {
        var found = await scope.ForTeacherAsync(classId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        await grades.RecomputeAsync(classId, null, cancellationToken);
        var students = await classes.GetClassStudentIdsAsync(classId, cancellationToken);
        var rows = await db.ClassGrades.AsNoTracking().Where(grade => grade.ClassId == classId && students.Contains(grade.StudentId)).ToListAsync(cancellationToken);
        var profiles = await users.GetUsersAsync([.. students], cancellationToken);
        return Result.Success<IReadOnlyList<ClassGradeDto>>(
        [
            .. rows.Select(row => ToDto(row, found.Value.Name, profiles.TryGetValue(row.StudentId, out var profile) ? profile.DisplayName : string.Empty))
                .OrderBy(row => row.StudentName, StringComparer.CurrentCultureIgnoreCase),
        ]);
    }

    public async Task<IReadOnlyList<ClassGradeDto>> MineAsync(CancellationToken cancellationToken)
    {
        var studentId = access.UserId;
        var result = new List<ClassGradeDto>();
        foreach (var classId in await classes.GetStudentClassIdsAsync(studentId, cancellationToken))
        {
            if (await classes.GetClassInfoAsync(classId, cancellationToken) is not { Status: not ClassStatus.Draft } info)
            {
                continue;
            }

            await grades.RecomputeAsync(classId, [studentId], cancellationToken);
            var row = await db.ClassGrades.AsNoTracking().FirstOrDefaultAsync(grade => grade.ClassId == classId && grade.StudentId == studentId, cancellationToken);
            if (row is not null)
            {
                result.Add(ToDto(row, info.Name, string.Empty));
            }
        }

        return result;
    }

    private static ClassGradeDto ToDto(ClassGrade row, string className, string studentName) =>
        new(row.ClassId, className, row.StudentId, studentName, row.FinalPct, row.Gpa04, row.Breakdown, row.ComputedAt);

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/classes/{id:guid}/grade-scheme", async (Guid id, GradesHandler handler, CancellationToken cancellationToken) =>
                (await handler.SchemeAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("GetGradeScheme")
            .WithSummary("Weights and 0–4 table of a class (teachers, admin)")
            .Produces<GradeSchemeDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/classes/{id:guid}/grade-scheme", async (Guid id, SaveGradeSchemeRequest request, GradesHandler handler, CancellationToken cancellationToken) =>
                (await handler.SaveSchemeAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("SaveGradeScheme")
            .WithSummary("Change the grade scheme; every grade of the class is recomputed (teachers, admin)")
            .Produces<GradeSchemeDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/classes/{id:guid}/grades", async (Guid id, GradesHandler handler, CancellationToken cancellationToken) =>
                (await handler.ClassGradesAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("GetClassGrades")
            .WithSummary("Grades 0–4 with breakdown per student (teachers, admin)")
            .Produces<IReadOnlyList<ClassGradeDto>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/me/grades", async (GradesHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.MineAsync(cancellationToken)))
            .RequireAuthorization(Policies.MuridOnly)
            .WithName("GetMyGrades")
            .WithSummary("My grade per class with breakdown (murid)")
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
