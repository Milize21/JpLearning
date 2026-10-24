using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Messaging;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Practice.Contracts;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Common;
using Nomax.Modules.Practice.Features.Questions;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Serialization;

namespace Nomax.Modules.Practice.Features.Drill;

/// <param name="AnswerId">UUIDv7 made by the client, so a resend is idempotent (BR-DRILL-07).</param>
/// <param name="InputMode">Informative; the server derives the input mode from the question kind and the answer.</param>
public sealed record SubmitAnswerRequest(Guid AnswerId, Guid QuestionId, JsonElement Answer, InputMode? InputMode = null, int? ResponseMs = null);

internal sealed class SubmitAnswerValidator : AbstractValidator<SubmitAnswerRequest>
{
    public SubmitAnswerValidator()
    {
        RuleFor(request => request.AnswerId).NotEmpty();
        RuleFor(request => request.QuestionId).NotEmpty();
        RuleFor(request => request.ResponseMs).InclusiveBetween(0, 3_600_000).When(request => request.ResponseMs is not null);
    }
}

/// <summary>
/// POST /api/practice/drill-sessions/{id}/answers — owner. Evaluated on the server (BR-DRILL-05/06), mastery updated
/// (BR-MASTERY-01) and <c>AnswerSubmitted</c> queued in the same transaction; the key and explanation are only sent now.
/// </summary>
internal sealed class SubmitAnswerHandler(
    PracticeDbContext db,
    PracticeAccess access,
    DrillPool pool,
    DrillLifecycle lifecycle,
    MasteryViews views,
    TimeProvider timeProvider)
{
    public async Task<Result<AnswerResultDto>> HandleAsync(Guid sessionId, SubmitAnswerRequest request, CancellationToken cancellationToken)
    {
        var session = await db.DrillSessions.FirstOrDefaultAsync(item => item.Id == sessionId && item.StudentId == access.UserId, cancellationToken);
        if (session is null)
        {
            return AppError.NotFound();
        }

        // BR-DRILL-07: the same answer id returns the stored result without counting twice.
        if (await ReplayAsync(session, request.AnswerId, cancellationToken) is { } replay)
        {
            return replay;
        }

        if (await lifecycle.EndIfIdleAsync(session, cancellationToken))
        {
            await db.SaveChangesAsync(cancellationToken);
            return DrillSessionsHandler.SessionEnded();
        }

        var scope = await pool.ScopeForAsync(session.StudentId, cancellationToken);
        var question = await pool.Query(scope, session.Category, session.Config)
            .FirstOrDefaultAsync(item => item.Id == request.QuestionId, cancellationToken);
        if (question is null)
        {
            var chapterId = await db.Questions.Where(item => item.Id == request.QuestionId).Select(item => (Guid?)item.ChapterId).FirstOrDefaultAsync(cancellationToken);
            return chapterId is { } chapter && !scope.AccessibleChapters.Contains(chapter)
                ? new AppError(ErrorCodes.ChapterLocked, "The question's chapter is no longer unlocked for you.")
                : AppError.NotFound("The question is not part of this session.");
        }

        var evaluated = PreviewAnswerHandler.Evaluate(question, request.Answer);
        if (!evaluated.IsSuccess)
        {
            return evaluated.Error;
        }

        var evaluation = evaluated.Value;
        var now = timeProvider.GetUtcNow();
        var (itemType, itemId) = MasteryKey.Of(question);
        var mastery = await db.ItemMasteries.FirstOrDefaultAsync(
            row => row.StudentId == session.StudentId && row.ItemType == itemType && row.ItemId == itemId, cancellationToken);
        if (mastery is null)
        {
            mastery = ItemMastery.Create(session.StudentId, itemType, itemId);
            db.ItemMasteries.Add(mastery);
        }

        var before = mastery.State;
        var after = MasteryRules.Apply(before, evaluation.IsCorrect, views.Settings);
        mastery.Apply(after, now, question.LevelId, question.ChapterId, question.Category, question.Tags.Where(tag => !QuestionRules.IsInternalTag(tag)));

        db.DrillAnswers.Add(DrillAnswer.Create(request.AnswerId, session.Id, question.Id, evaluation.InputMode, request.Answer, evaluation.IsCorrect, request.ResponseMs));
        db.AddIntegrationEvent(
            new AnswerSubmitted(
                request.AnswerId, session.Id, session.StudentId, question.LevelId, question.ChapterId, question.Id,
                EnumSnakeCase.ToValue(itemType), itemId, EnumSnakeCase.ToValue(question.Category), evaluation.IsCorrect,
                EnumSnakeCase.ToValue(evaluation.InputMode), now),
            timeProvider);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent resend of the same answer won the race: answer like a replay.
            db.ChangeTracker.Clear();
            var stored = await db.DrillSessions.FirstAsync(item => item.Id == sessionId, cancellationToken);
            return await ReplayAsync(stored, request.AnswerId, cancellationToken)
                ?? AppError.Conflict("The answer could not be saved; send it again.");
        }

        return new AnswerResultDto(
            evaluation.IsCorrect,
            evaluation.CorrectAnswer,
            question.Explanation,
            new MasteryChangeDto(before.Mastery, after.Mastery, MasteryRules.StatusOf(after, views.Settings)));
    }

    /// <summary>A stored answer with this id: its result for this session, or 409 when the id belongs elsewhere.</summary>
    private async Task<Result<AnswerResultDto>?> ReplayAsync(DrillSession session, Guid answerId, CancellationToken cancellationToken)
    {
        var stored = await db.DrillAnswers.AsNoTracking().FirstOrDefaultAsync(answer => answer.Id == answerId, cancellationToken);
        if (stored is null)
        {
            return null;
        }

        if (stored.SessionId != session.Id)
        {
            return AppError.Conflict("This answer id was already used.");
        }

        var question = await db.Questions.AsNoTracking().FirstAsync(item => item.Id == stored.QuestionId, cancellationToken);
        var evaluation = AnswerEvaluator.Evaluate(question.Kind, question.Payload, stored.Answer);
        var (itemType, itemId) = MasteryKey.Of(question);
        var mastery = await db.ItemMasteries.AsNoTracking().FirstOrDefaultAsync(
            row => row.StudentId == session.StudentId && row.ItemType == itemType && row.ItemId == itemId, cancellationToken);
        var state = mastery?.State ?? MasteryState.New;
        // The stored row only knows the current mastery, so a replay reports it as both before and after.
        return new AnswerResultDto(stored.IsCorrect, evaluation.CorrectAnswer, question.Explanation,
            new MasteryChangeDto(state.Mastery, state.Mastery, MasteryRules.StatusOf(state, views.Settings)));
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/drill-sessions/{id:guid}/answers", async (Guid id, SubmitAnswerRequest request, SubmitAnswerHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.MuridOnly)
            .WithValidation<SubmitAnswerRequest>()
            .WithName("SubmitDrillAnswer")
            .WithSummary("Answer a question; idempotent per answer id (owner)")
            .Produces<AnswerResultDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
