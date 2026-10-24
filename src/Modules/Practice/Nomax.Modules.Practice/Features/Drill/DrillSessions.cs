using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Common;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Practice.Features.Drill;

/// <param name="ChapterIds">Empty or absent = every accessible chapter (BR-DRILL-02).</param>
/// <param name="InputMode"><c>choice</c> (all kinds except handwriting), <c>type</c> (typed answers only) or <c>handwrite</c> (phase 5).</param>
public sealed record StartDrillRequest(
    Category Category,
    DrillMode Mode = DrillMode.Normal,
    InputMode InputMode = InputMode.Choice,
    IReadOnlyList<Guid>? ChapterIds = null,
    Guid? ClassId = null);

public sealed record StartDrillResponse(Guid SessionId, int PoolSize, string? Fallback, IReadOnlyList<SafeQuestion> Batch);

/// <param name="MasteredItems">Items of this session that are now mastered (BR-MASTERY-02).</param>
/// <param name="WeakItems">Items of this session that are now weak.</param>
public sealed record DrillSummaryDto(
    Guid SessionId,
    Category Category,
    int AnswerCount,
    int CorrectCount,
    decimal Accuracy,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    IReadOnlyList<MasteryItemDto> MasteredItems,
    IReadOnlyList<MasteryItemDto> WeakItems);

internal sealed class StartDrillValidator : AbstractValidator<StartDrillRequest>
{
    public StartDrillValidator()
    {
        RuleFor(request => request.Category).IsInEnum();
        RuleFor(request => request.Mode).IsInEnum();
        RuleFor(request => request.InputMode).Must(mode => mode is InputMode.Choice or InputMode.Type or InputMode.Handwrite)
            .WithMessage("Must be choice, type or handwrite.");
        RuleFor(request => request.ChapterIds).Must(ids => ids is null || ids.Count <= 200).WithMessage("At most 200 chapters.");
    }
}

/// <summary>
/// POST /drill-sessions (murid), GET /drill-sessions/{id}/next and POST /drill-sessions/{id}/end (owner). Drilling has no
/// limit and no grade (BR-DRILL-01); sessions belong to their student, anyone else gets 404.
/// </summary>
internal sealed class DrillSessionsHandler(
    PracticeDbContext db,
    PracticeAccess access,
    DrillPool pool,
    DrillBatches batches,
    DrillLifecycle lifecycle,
    MasteryViews views,
    IClassAccessReader classes,
    TimeProvider timeProvider)
{
    public async Task<Result<StartDrillResponse>> StartAsync(StartDrillRequest request, CancellationToken cancellationToken)
    {
        var studentId = access.UserId;
        var scope = await pool.ScopeForAsync(studentId, cancellationToken);
        var chapterIds = (request.ChapterIds ?? []).Distinct().ToList();
        if (chapterIds.Any(id => !scope.AccessibleChapters.Contains(id)))
        {
            return new AppError(ErrorCodes.ChapterLocked, "A chosen chapter is not unlocked for any of your classes.");
        }

        if (request.ClassId is { } classId && !await classes.IsStudentOfAsync(studentId, classId, cancellationToken))
        {
            return AppError.NotFound("No such class.");
        }

        var config = new DrillConfig(chapterIds, request.Mode, request.InputMode);
        if (!await pool.Query(scope, request.Category, config).AnyAsync(cancellationToken))
        {
            return new AppError(ErrorCodes.EmptyPool, "No published questions match this category and these chapters.");
        }

        var session = DrillSession.Start(Uuid7.New(timeProvider), studentId, request.ClassId, request.Category, config, timeProvider.GetUtcNow());
        db.DrillSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        var (batch, poolSize) = await batches.NextAsync(session, null, cancellationToken);
        return new StartDrillResponse(session.Id, poolSize, batch.Fallback, batch.Questions);
    }

    public async Task<Result<DrillBatchDto>> NextAsync(Guid id, int? count, CancellationToken cancellationToken)
    {
        var found = await OwnAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var session = found.Value;
        if (await lifecycle.EndIfIdleAsync(session, cancellationToken))
        {
            await db.SaveChangesAsync(cancellationToken);
            return SessionEnded();
        }

        var (batch, poolSize) = await batches.NextAsync(session, count, cancellationToken);
        return poolSize == 0
            ? new AppError(ErrorCodes.EmptyPool, "No questions are left for this session, e.g. after a chapter was closed.")
            : batch;
    }

    public async Task<Result<DrillSummaryDto>> EndAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await OwnAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var session = found.Value;
        if (session.EndedAt is null)
        {
            await lifecycle.EndAsync(session, timeProvider.GetUtcNow(), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return await SummaryAsync(session, cancellationToken);
    }

    private async Task<DrillSummaryDto> SummaryAsync(DrillSession session, CancellationToken cancellationToken)
    {
        var answers = await db.DrillAnswers.AsNoTracking()
            .Where(answer => answer.SessionId == session.Id)
            .Join(db.Questions.AsNoTracking(), answer => answer.QuestionId, question => question.Id, (answer, question) => new { answer.IsCorrect, question.ItemType, question.ItemId, QuestionId = question.Id })
            .ToListAsync(cancellationToken);
        var keys = answers.Select(answer => (MasteryKey.TypeOf(answer.ItemType), answer.ItemId ?? answer.QuestionId)).Distinct().ToList();
        var items = await views.ItemsAsync(session.StudentId, keys, cancellationToken);
        var correct = answers.Count(answer => answer.IsCorrect);
        return new DrillSummaryDto(
            session.Id,
            session.Category,
            answers.Count,
            correct,
            answers.Count == 0 ? 0m : Math.Round((decimal)correct / answers.Count, 3, MidpointRounding.AwayFromZero),
            session.StartedAt,
            session.EndedAt,
            [.. items.Where(item => item.Status == MasteryStatus.Mastered)],
            [.. items.Where(item => item.Status == MasteryStatus.Weak).OrderBy(item => item.Mastery)]);
    }

    private async Task<Result<DrillSession>> OwnAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await db.DrillSessions.FirstOrDefaultAsync(item => item.Id == id && item.StudentId == access.UserId, cancellationToken);
        return session is null ? AppError.NotFound() : session;
    }

    internal static AppError SessionEnded() =>
        new(ErrorCodes.SessionEnded, "The drill session has ended; start a new one.");

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/drill-sessions", async (StartDrillRequest request, DrillSessionsHandler handler, CancellationToken cancellationToken) =>
                (await handler.StartAsync(request, cancellationToken))
                    .ToHttpResult(started => TypedResults.Created($"/api/practice/drill-sessions/{started.SessionId}", started)))
            .RequireAuthorization(Policies.MuridOnly)
            .WithValidation<StartDrillRequest>()
            .WithName("StartDrillSession")
            .WithSummary("Start drilling: category, chapters, mode and input mode (murid)")
            .Produces<StartDrillResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/drill-sessions/{id:guid}/next", async (Guid id, [FromQuery(Name = "count")] int? count, DrillSessionsHandler handler, CancellationToken cancellationToken) =>
                (await handler.NextAsync(id, count, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.MuridOnly)
            .WithName("NextDrillBatch")
            .WithSummary("The next batch of questions, without answer keys (owner)")
            .Produces<DrillBatchDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/drill-sessions/{id:guid}/end", async (Guid id, DrillSessionsHandler handler, CancellationToken cancellationToken) =>
                (await handler.EndAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.MuridOnly)
            .WithName("EndDrillSession")
            .WithSummary("End the session and get its summary (owner)")
            .Produces<DrillSummaryDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
