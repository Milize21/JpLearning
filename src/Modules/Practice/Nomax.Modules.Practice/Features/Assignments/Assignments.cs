using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Messaging;
using Nomax.BuildingBlocks.Persistence;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Identity.Contracts;
using Nomax.Modules.Practice.Contracts;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Common;
using Nomax.Modules.Practice.Features.Grades;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Features.Assignments;

public sealed record SaveAssignmentRequest(
    TextBlock Title,
    RichText Instructions,
    DateTimeOffset DueAt,
    bool AllowResubmit = false,
    Guid? ChapterId = null,
    uint? Version = null);

public sealed record SubmitAssignmentRequest(string? Text = null, IReadOnlyList<Guid>? FileIds = null);

public sealed record GradeSubmissionRequest(decimal GradePct, string? Feedback = null);

public sealed record AssignmentDto(
    Guid Id,
    Guid ClassId,
    Guid? ChapterId,
    TextBlock Title,
    RichText Instructions,
    DateTimeOffset DueAt,
    bool AllowResubmit,
    PublishStatus Status,
    uint Version,
    int SubmissionCount,
    int GradedCount)
{
    public static AssignmentDto From(Assignment item, int submissions, int graded) =>
        new(item.Id, item.ClassId, item.ChapterId, item.Title, item.Instructions, item.DueAt, item.AllowResubmit, item.Status, item.Version, submissions, graded);
}

public sealed record SubmissionFileDto(Guid Id, string Name, long SizeBytes);

/// <param name="IsLate">BR-TUGAS-03: submitted after the due time (D-20).</param>
public sealed record SubmissionDto(
    Guid Id,
    Guid AssignmentId,
    Guid StudentId,
    string StudentName,
    string? Text,
    IReadOnlyList<SubmissionFileDto> Files,
    DateTimeOffset SubmittedAt,
    bool IsLate,
    decimal? GradePct,
    string? Feedback,
    DateTimeOffset? GradedAt);

public sealed record StudentAssignmentDto(
    Guid Id,
    Guid ClassId,
    string ClassName,
    TextBlock Title,
    RichText Instructions,
    DateTimeOffset DueAt,
    bool AllowResubmit,
    SubmissionDto? Submission);

internal sealed class SaveAssignmentValidator : AbstractValidator<SaveAssignmentRequest>
{
    public SaveAssignmentValidator()
    {
        RuleFor(request => request.Title).ValidTextBlock(TextBlockUsage.Title);
        RuleFor(request => request.Instructions).ValidRichText();
    }
}

internal sealed class SubmitAssignmentValidator : AbstractValidator<SubmitAssignmentRequest>
{
    public SubmitAssignmentValidator()
    {
        RuleFor(request => request.Text).MaximumLength(Submission.TextMaxLength);
        RuleFor(request => request.FileIds).Must(ids => ids is null || (ids.Count <= Submission.MaxFiles && ids.Distinct().Count() == ids.Count))
            .WithMessage($"At most {Submission.MaxFiles} distinct files.");
        RuleFor(request => request).Must(request => AssignmentRules.HasContent(request.Text, request.FileIds ?? []))
            .WithName("text").WithMessage("Write a text or attach at least one file.");
    }
}

internal sealed class GradeSubmissionValidator : AbstractValidator<GradeSubmissionRequest>
{
    public GradeSubmissionValidator()
    {
        RuleFor(request => request.GradePct).InclusiveBetween(0, 100);
        RuleFor(request => request.Feedback).MaximumLength(Submission.FeedbackMaxLength);
    }
}

/// <summary>
/// Assignments (docs/05 §7, BR-TUGAS-01…04): teachers and admins manage them per class; students of the class see the
/// published ones, submit text and/or files (tus uploads with purpose <c>submission</c>) and read the grade and feedback.
/// </summary>
internal sealed class AssignmentsHandler(
    PracticeDbContext db,
    PracticeAccess access,
    ClassScope scope,
    IClassAccessReader classes,
    IContentCatalog catalog,
    IStoredFileService files,
    IUserDirectory users,
    GradeService grades,
    TimeProvider timeProvider)
{
    public async Task<Result<IReadOnlyList<AssignmentDto>>> ListAsync(Guid classId, CancellationToken cancellationToken)
    {
        var found = await scope.ForTeacherAsync(classId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var list = await db.Assignments.AsNoTracking().Where(item => item.ClassId == classId).OrderByDescending(item => item.DueAt).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<AssignmentDto>>(await DtosAsync(list, cancellationToken));
    }

    public async Task<Result<AssignmentDto>> CreateAsync(Guid classId, SaveAssignmentRequest request, CancellationToken cancellationToken)
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

        if (await CheckChapterAsync(found.Value.BookId, request.ChapterId, cancellationToken) is { } chapterError)
        {
            return chapterError;
        }

        var item = Assignment.Create(Uuid7.New(timeProvider), classId, request.ChapterId, request.Title, request.Instructions, request.DueAt.ToUniversalTime(), request.AllowResubmit, access.UserId);
        db.Assignments.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return AssignmentDto.From(item, 0, 0);
    }

    public async Task<Result<AssignmentDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await ManagedAsync(id, cancellationToken);
        return found.IsSuccess ? (await DtosAsync([found.Value], cancellationToken))[0] : found.Error;
    }

    public async Task<Result<AssignmentDto>> UpdateAsync(Guid id, SaveAssignmentRequest request, CancellationToken cancellationToken)
    {
        var found = await ManagedAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var item = found.Value;
        if (db.ExpectVersion(item, request.Version ?? 0) is { } stale)
        {
            return stale;
        }

        var info = await classes.GetClassInfoAsync(item.ClassId, cancellationToken);
        if (info is not null && await CheckChapterAsync(info.BookId, request.ChapterId, cancellationToken) is { } chapterError)
        {
            return chapterError;
        }

        item.Update(request.Title, request.ChapterId, request.Instructions, request.DueAt.ToUniversalTime(), request.AllowResubmit);
        if (await db.SaveChangesOrConflictAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        // A new due time changes which missing submissions count as 0 (BR-NILAI-01).
        await grades.RecomputeAsync(item.ClassId, null, cancellationToken);
        return (await DtosAsync([item], cancellationToken))[0];
    }

    public async Task<Result<AssignmentDto>> ChangeStatusAsync(Guid id, PublishStatus status, CancellationToken cancellationToken)
    {
        var found = await ManagedAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var item = found.Value;
        if (status == PublishStatus.Published && item.Status != PublishStatus.Draft)
        {
            return AppError.InvalidState("Only draft assignments can be published.");
        }

        item.ChangeStatus(status);
        await db.SaveChangesAsync(cancellationToken);
        await grades.RecomputeAsync(item.ClassId, null, cancellationToken);
        return (await DtosAsync([item], cancellationToken))[0];
    }

    /// <summary>PUT /assignments/{id}/submission — BR-TUGAS-01 content and files, BR-TUGAS-02 resend, BR-TUGAS-03 late.</summary>
    public async Task<Result<SubmissionDto>> SubmitAsync(Guid id, SubmitAssignmentRequest request, CancellationToken cancellationToken)
    {
        var item = await db.Assignments.AsNoTracking().FirstOrDefaultAsync(entry => entry.Id == id && entry.Status == PublishStatus.Published, cancellationToken);
        if (item is null || !(await scope.ForMemberAsync(item.ClassId, cancellationToken)).IsSuccess)
        {
            return AppError.NotFound();
        }

        if ((await classes.GetClassInfoAsync(item.ClassId, cancellationToken))?.Status == ClassStatus.Archived)
        {
            return AppError.InvalidState("The class is archived and read only.");
        }

        var studentId = access.UserId;
        var fileIds = (request.FileIds ?? []).ToList();
        var stored = await files.GetFilesAsync(fileIds, cancellationToken);
        foreach (var fileId in fileIds)
        {
            if (!stored.TryGetValue(fileId, out var file) || file.UploadedBy != studentId || file.Purpose != "submission")
            {
                return AppError.Validation(new Dictionary<string, string[]> { ["fileIds"] = ["Use your own uploads for this assignment."] });
            }

            if (file.Status != "ready")
            {
                return new AppError(ErrorCodes.FileNotReady, "A file is still uploading.");
            }
        }

        var text = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim();
        var now = timeProvider.GetUtcNow();
        var submission = await db.Submissions.FirstOrDefaultAsync(entry => entry.AssignmentId == id && entry.StudentId == studentId, cancellationToken);
        if (submission is null)
        {
            submission = Submission.Create(Uuid7.New(timeProvider), id, studentId, text, fileIds, now);
            db.Submissions.Add(submission);
        }
        else if (AssignmentRules.CanResubmit(item.AllowResubmit, submission.GradePct))
        {
            submission.Resubmit(text, fileIds, now);
        }
        else
        {
            return new AppError(ErrorCodes.AlreadySubmitted, "This assignment was already submitted.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return await SubmissionDtoAsync(submission, item.DueAt, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SubmissionDto>>> SubmissionsAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await ManagedAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var list = await db.Submissions.AsNoTracking().Where(entry => entry.AssignmentId == id).OrderBy(entry => entry.SubmittedAt).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<SubmissionDto>>(await SubmissionDtosAsync(list, found.Value.DueAt, cancellationToken));
    }

    /// <summary>The submission for its student, the teachers of the class and admins (BR-FILE-04).</summary>
    public async Task<Result<(Submission Submission, Assignment Assignment)>> VisibleSubmissionAsync(Guid id, CancellationToken cancellationToken)
    {
        var submission = await db.Submissions.FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        if (submission is null)
        {
            return AppError.NotFound();
        }

        var item = await db.Assignments.AsNoTracking().FirstAsync(entry => entry.Id == submission.AssignmentId, cancellationToken);
        var allowed = access.IsStudent
            ? submission.StudentId == access.UserId
            : (await scope.ForTeacherAsync(item.ClassId, cancellationToken)).IsSuccess;
        return allowed ? (submission, item) : AppError.NotFound();
    }

    public async Task<Result<SubmissionDto>> GetSubmissionAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await VisibleSubmissionAsync(id, cancellationToken);
        return found.IsSuccess ? await SubmissionDtoAsync(found.Value.Submission, found.Value.Assignment.DueAt, cancellationToken) : found.Error;
    }

    public async Task<Result<string>> DownloadUrlAsync(Guid id, Guid fileId, CancellationToken cancellationToken)
    {
        var found = await VisibleSubmissionAsync(id, cancellationToken);
        if (!found.IsSuccess || !found.Value.Submission.FileIds.Contains(fileId))
        {
            return AppError.NotFound();
        }

        return files.CreateDownloadUrl(fileId);
    }

    /// <summary>BR-TUGAS-04: grade and feedback by a teacher of the class or an admin; grades may change later.</summary>
    public async Task<Result<SubmissionDto>> GradeAsync(Guid id, GradeSubmissionRequest request, CancellationToken cancellationToken)
    {
        var found = await VisibleSubmissionAsync(id, cancellationToken);
        if (!found.IsSuccess || access.IsStudent)
        {
            return AppError.NotFound();
        }

        var (submission, item) = found.Value;
        var now = timeProvider.GetUtcNow();
        submission.Grade(Math.Round(request.GradePct, 2, MidpointRounding.AwayFromZero), string.IsNullOrWhiteSpace(request.Feedback) ? null : request.Feedback.Trim(), access.UserId, now);
        db.AddIntegrationEvent(
            new SubmissionGraded(submission.Id, item.Id, item.ClassId, submission.StudentId, submission.GradePct!.Value, access.UserId, now),
            timeProvider);
        await db.SaveChangesAsync(cancellationToken);
        await grades.RecomputeAsync(item.ClassId, [submission.StudentId], cancellationToken);
        return await SubmissionDtoAsync(submission, item.DueAt, cancellationToken);
    }

    public async Task<IReadOnlyList<StudentAssignmentDto>> MineAsync(CancellationToken cancellationToken)
    {
        var studentId = access.UserId;
        var infos = new List<ClassInfo>();
        foreach (var classId in await classes.GetStudentClassIdsAsync(studentId, cancellationToken))
        {
            if (await classes.GetClassInfoAsync(classId, cancellationToken) is { Status: not ClassStatus.Draft } info)
            {
                infos.Add(info);
            }
        }

        var classIds = infos.Select(info => info.Id).ToList();
        var list = await db.Assignments.AsNoTracking()
            .Where(item => classIds.Contains(item.ClassId) && item.Status == PublishStatus.Published)
            .OrderBy(item => item.DueAt).ToListAsync(cancellationToken);
        var ids = list.Select(item => item.Id).ToList();
        var mine = await db.Submissions.AsNoTracking().Where(entry => ids.Contains(entry.AssignmentId) && entry.StudentId == studentId).ToListAsync(cancellationToken);
        var dtos = new Dictionary<Guid, SubmissionDto>();
        foreach (var submission in mine)
        {
            dtos[submission.AssignmentId] = await SubmissionDtoAsync(submission, list.First(item => item.Id == submission.AssignmentId).DueAt, cancellationToken);
        }

        var names = infos.ToDictionary(info => info.Id, info => info.Name);
        return
        [
            .. list.Select(item => new StudentAssignmentDto(
                item.Id, item.ClassId, names.GetValueOrDefault(item.ClassId, string.Empty), item.Title, item.Instructions, item.DueAt,
                item.AllowResubmit, dtos.GetValueOrDefault(item.Id))),
        ];
    }

    private async Task<Result<Assignment>> ManagedAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.Assignments.FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        if (item is null)
        {
            return AppError.NotFound();
        }

        return (await scope.ForTeacherAsync(item.ClassId, cancellationToken)).IsSuccess ? item : AppError.NotFound();
    }

    private async Task<AppError?> CheckChapterAsync(Guid bookId, Guid? chapterId, CancellationToken cancellationToken) =>
        chapterId is not { } id || (await catalog.GetChapterAsync(id, cancellationToken))?.BookId == bookId
            ? null
            : AppError.Validation(new Dictionary<string, string[]> { ["chapterId"] = ["Must be a chapter of the class's book."] });

    private async Task<IReadOnlyList<AssignmentDto>> DtosAsync(IReadOnlyList<Assignment> list, CancellationToken cancellationToken)
    {
        var ids = list.Select(item => item.Id).ToList();
        var counts = await db.Submissions.AsNoTracking().Where(entry => ids.Contains(entry.AssignmentId))
            .GroupBy(entry => entry.AssignmentId)
            .Select(group => new { group.Key, Count = group.Count(), Graded = group.Count(entry => entry.GradePct != null) })
            .ToDictionaryAsync(row => row.Key, cancellationToken);
        return [.. list.Select(item => AssignmentDto.From(item, counts.TryGetValue(item.Id, out var row) ? row.Count : 0, row?.Graded ?? 0))];
    }

    private async Task<SubmissionDto> SubmissionDtoAsync(Submission submission, DateTimeOffset dueAt, CancellationToken cancellationToken) =>
        (await SubmissionDtosAsync([submission], dueAt, cancellationToken))[0];

    private async Task<IReadOnlyList<SubmissionDto>> SubmissionDtosAsync(IReadOnlyList<Submission> list, DateTimeOffset dueAt, CancellationToken cancellationToken)
    {
        var fileInfos = await files.GetFilesAsync([.. list.SelectMany(entry => entry.FileIds).Distinct()], cancellationToken);
        var profiles = await users.GetUsersAsync([.. list.Select(entry => entry.StudentId).Distinct()], cancellationToken);
        return
        [
            .. list.Select(entry => new SubmissionDto(
                entry.Id, entry.AssignmentId, entry.StudentId,
                profiles.TryGetValue(entry.StudentId, out var profile) ? profile.DisplayName : string.Empty,
                entry.Text,
                [.. entry.FileIds.Where(fileInfos.ContainsKey).Select(fileId => new SubmissionFileDto(fileId, fileInfos[fileId].OriginalName, fileInfos[fileId].SizeBytes))],
                entry.SubmittedAt, AssignmentRules.IsLate(entry.SubmittedAt, dueAt), entry.GradePct, entry.Feedback, entry.GradedAt)),
        ];
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/classes/{id:guid}/assignments", async (Guid id, AssignmentsHandler handler, CancellationToken cancellationToken) =>
                (await handler.ListAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("ListClassAssignments")
            .WithSummary("Assignments of a class (teachers, admin); students use /me/assignments")
            .Produces<IReadOnlyList<AssignmentDto>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/classes/{id:guid}/assignments", async (Guid id, SaveAssignmentRequest request, AssignmentsHandler handler, CancellationToken cancellationToken) =>
                (await handler.CreateAsync(id, request, cancellationToken)).ToHttpResult(item => TypedResults.Created($"/api/practice/assignments/{item.Id}", item)))
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<SaveAssignmentRequest>()
            .WithName("CreateAssignment")
            .WithSummary("Create a draft assignment (teachers, admin)")
            .Produces<AssignmentDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/assignments/{id:guid}", async (Guid id, AssignmentsHandler handler, CancellationToken cancellationToken) =>
                (await handler.GetAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("GetAssignment")
            .WithSummary("One assignment (teachers, admin)")
            .Produces<AssignmentDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/assignments/{id:guid}", async (Guid id, SaveAssignmentRequest request, AssignmentsHandler handler, CancellationToken cancellationToken) =>
                (await handler.UpdateAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<SaveAssignmentRequest>()
            .WithName("UpdateAssignment")
            .WithSummary("Change an assignment (teachers, admin)")
            .Produces<AssignmentDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/assignments/{id:guid}/publish", async (Guid id, AssignmentsHandler handler, CancellationToken cancellationToken) =>
                (await handler.ChangeStatusAsync(id, PublishStatus.Published, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("PublishAssignment")
            .WithSummary("Publish an assignment (teachers, admin)")
            .Produces<AssignmentDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/assignments/{id:guid}/archive", async (Guid id, AssignmentsHandler handler, CancellationToken cancellationToken) =>
                (await handler.ChangeStatusAsync(id, PublishStatus.Archived, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("ArchiveAssignment")
            .WithSummary("Archive an assignment (teachers, admin)")
            .Produces<AssignmentDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/assignments/{id:guid}/submission", async (Guid id, SubmitAssignmentRequest request, AssignmentsHandler handler, CancellationToken cancellationToken) =>
                (await handler.SubmitAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.MuridOnly)
            .WithValidation<SubmitAssignmentRequest>()
            .WithName("SubmitAssignment")
            .WithSummary("Submit or resubmit (murid of the class)")
            .Produces<SubmissionDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/assignments/{id:guid}/submissions", async (Guid id, AssignmentsHandler handler, CancellationToken cancellationToken) =>
                (await handler.SubmissionsAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("ListSubmissions")
            .WithSummary("Submissions of an assignment (teachers, admin)")
            .Produces<IReadOnlyList<SubmissionDto>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/submissions/{id:guid}", async (Guid id, AssignmentsHandler handler, CancellationToken cancellationToken) =>
                (await handler.GetSubmissionAsync(id, cancellationToken)).ToOkResult())
            .WithName("GetSubmission")
            .WithSummary("One submission (its student, teachers, admin)")
            .Produces<SubmissionDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/submissions/{id:guid}/files/{fileId:guid}/download", async (Guid id, Guid fileId, bool? redirect, AssignmentsHandler handler, CancellationToken cancellationToken) =>
                (await handler.DownloadUrlAsync(id, fileId, cancellationToken)).ToHttpResult(url =>
                    redirect == false ? (IResult)TypedResults.Ok(new SubmissionDownloadDto(url)) : TypedResults.Redirect(url)))
            .WithName("DownloadSubmissionFile")
            .WithSummary("302 to a signed link valid for 5 minutes (its student, teachers, admin; BR-FILE-04)")
            .Produces<SubmissionDownloadDto>()
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/submissions/{id:guid}/grade", async (Guid id, GradeSubmissionRequest request, AssignmentsHandler handler, CancellationToken cancellationToken) =>
                (await handler.GradeAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<GradeSubmissionRequest>()
            .WithName("GradeSubmission")
            .WithSummary("Grade 0–100 with feedback (teachers of the class, admin)")
            .Produces<SubmissionDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/me/assignments", async (AssignmentsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.MineAsync(cancellationToken)))
            .RequireAuthorization(Policies.MuridOnly)
            .WithName("ListMyAssignments")
            .WithSummary("Published assignments of my classes with my submission (murid)")
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}

public sealed record SubmissionDownloadDto(string Url);
