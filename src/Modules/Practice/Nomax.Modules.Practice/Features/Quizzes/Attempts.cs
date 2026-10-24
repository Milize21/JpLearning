using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Practice.Features.Quizzes;

public sealed record SaveAttemptAnswerRequest(JsonElement Answer);

public sealed record GradeAnswerRequest(decimal Points);

internal sealed class GradeAnswerValidator : AbstractValidator<GradeAnswerRequest>
{
    public GradeAnswerValidator() => RuleFor(request => request.Points).GreaterThanOrEqualTo(0);
}

/// <summary>
/// Taking quizzes (docs/05 §7, BR-KUIS-01…09): POST /quizzes/{id}/attempts (start or resume), GET /attempts/{id},
/// PUT /attempts/{id}/answers/{questionId} (autosave), POST /attempts/{id}/submit; staff: GET /quizzes/{id}/attempts and
/// POST /attempts/{id}/answers/{questionId}/grade for long answers; students: GET /me/quizzes.
/// </summary>
internal sealed class AttemptsHandler(
    PracticeDbContext db,
    PracticeAccess access,
    ClassScope scope,
    IClassAccessReader classes,
    IUserDirectory users,
    AttemptGrading grading,
    TimeProvider timeProvider)
{
    public async Task<Result<AttemptDto>> StartAsync(Guid quizId, CancellationToken cancellationToken)
    {
        var quiz = await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == quizId && item.Status == PublishStatus.Published, cancellationToken);
        if (quiz is null || !(await scope.ForMemberAsync(quiz.ClassId, cancellationToken)).IsSuccess)
        {
            return AppError.NotFound();
        }

        var studentId = access.UserId;
        var running = await db.QuizAttempts.FirstOrDefaultAsync(
            attempt => attempt.QuizId == quizId && attempt.StudentId == studentId && attempt.Status == AttemptStatus.InProgress, cancellationToken);
        if (running is not null)
        {
            // BR-KUIS-02: starting again resumes the running attempt, unless its time is over (BR-KUIS-03).
            if (!QuizRules.IsExpired(running.ExpiresAt, timeProvider.GetUtcNow(), grading.Grace))
            {
                return await ViewAsync(running, quiz, cancellationToken);
            }

            await grading.SubmitAsync(running, cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        if (!QuizRules.IsOpen(quiz.OpenAt, quiz.CloseAt, now))
        {
            return new AppError(ErrorCodes.QuizNotOpen, "The quiz is not open now.");
        }

        var used = await db.QuizAttempts.CountAsync(attempt => attempt.QuizId == quizId && attempt.StudentId == studentId, cancellationToken);
        if (used >= quiz.MaxAttempts)
        {
            return new AppError(ErrorCodes.AttemptsExhausted, "No attempts left for this quiz.");
        }

        var questionIds = await db.QuizQuestions.AsNoTracking().Where(item => item.QuizId == quizId)
            .OrderBy(item => item.SortOrder).Select(item => item.QuestionId).ToListAsync(cancellationToken);
        var id = Uuid7.New(timeProvider);
        // BR-KUIS-04: a permutation per attempt, seeded by its id so a reload shows the same order.
        var order = quiz.Shuffle ? Shuffled(questionIds, new Random(SafeQuestions.StableSeed(id, quizId))) : questionIds;
        var attempt = QuizAttempt.Start(id, quizId, studentId, used + 1, now, QuizRules.ExpiresAt(now, quiz.TimeLimitMin, quiz.CloseAt), order);
        db.QuizAttempts.Add(attempt);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two tabs started at once: the unique index on running attempts let one win; resume it.
            db.ChangeTracker.Clear();
            var winner = await db.QuizAttempts.FirstAsync(item => item.QuizId == quizId && item.StudentId == studentId && item.Status == AttemptStatus.InProgress, cancellationToken);
            return await ViewAsync(winner, quiz, cancellationToken);
        }

        return await ViewAsync(attempt, quiz, cancellationToken);
    }

    public async Task<Result<AttemptDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await VisibleAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var (attempt, quiz) = found.Value;
        if (attempt.Status == AttemptStatus.InProgress && QuizRules.IsExpired(attempt.ExpiresAt, timeProvider.GetUtcNow(), grading.Grace))
        {
            await grading.SubmitAsync(attempt, cancellationToken);
        }

        return await ViewAsync(attempt, quiz, cancellationToken);
    }

    /// <summary>BR-KUIS-06: the last save wins while the attempt runs and its time is not over.</summary>
    public async Task<Result> SaveAnswerAsync(Guid id, Guid questionId, SaveAttemptAnswerRequest request, CancellationToken cancellationToken)
    {
        var found = await OwnRunningAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var attempt = found.Value;
        if (!attempt.QuestionOrder.Contains(questionId))
        {
            return AppError.NotFound("The question is not part of this attempt.");
        }

        if (request.Answer.ValueKind != JsonValueKind.Object || request.Answer.GetRawText().Length > 30_000)
        {
            return AppError.Validation(new Dictionary<string, string[]> { ["answer"] = ["Must be an answer object (docs/03 §8.2)."] });
        }

        var answer = await db.AttemptAnswers.FirstOrDefaultAsync(item => item.AttemptId == id && item.QuestionId == questionId, cancellationToken);
        if (answer is null)
        {
            db.AttemptAnswers.Add(AttemptAnswer.Create(id, questionId, request.Answer));
        }
        else
        {
            answer.Save(request.Answer);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<AttemptDto>> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        var attempt = await db.QuizAttempts.FirstOrDefaultAsync(item => item.Id == id && item.StudentId == access.UserId, cancellationToken);
        if (attempt is null)
        {
            return AppError.NotFound();
        }

        if (attempt.Status != AttemptStatus.InProgress)
        {
            return AppError.InvalidState("The attempt was already submitted.");
        }

        await grading.SubmitAsync(attempt, cancellationToken);
        var quiz = await db.Quizzes.AsNoTracking().FirstAsync(item => item.Id == attempt.QuizId, cancellationToken);
        return await ViewAsync(attempt, quiz, cancellationToken);
    }

    /// <summary>Manual grading of a long answer by a teacher of the class or an admin (BR-KUIS-07).</summary>
    public async Task<Result<AttemptDto>> GradeAnswerAsync(Guid id, Guid questionId, GradeAnswerRequest request, CancellationToken cancellationToken)
    {
        var attempt = await db.QuizAttempts.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        var quiz = attempt is null ? null : await db.Quizzes.AsNoTracking().FirstAsync(item => item.Id == attempt.QuizId, cancellationToken);
        if (attempt is null || quiz is null || !(await scope.ForTeacherAsync(quiz.ClassId, cancellationToken)).IsSuccess)
        {
            return AppError.NotFound();
        }

        var item = await db.QuizQuestions.AsNoTracking().FirstOrDefaultAsync(entry => entry.QuizId == quiz.Id && entry.QuestionId == questionId, cancellationToken);
        var answer = await db.AttemptAnswers.FirstOrDefaultAsync(entry => entry.AttemptId == id && entry.QuestionId == questionId, cancellationToken);
        var kind = await db.Questions.AsNoTracking().Where(question => question.Id == questionId).Select(question => question.Kind).FirstOrDefaultAsync(cancellationToken);
        if (item is null || answer is null || attempt.Status == AttemptStatus.InProgress || kind != QuestionKind.LongAnswer)
        {
            return AppError.InvalidState("Only submitted long answers are graded by hand.");
        }

        if (request.Points > item.Points)
        {
            return AppError.Validation(new Dictionary<string, string[]> { ["points"] = [$"At most {item.Points}."] });
        }

        answer.GradeManually(Math.Round(request.Points, 2), item.Points, access.UserId, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        if (attempt.Status == AttemptStatus.Graded)
        {
            // A corrected grade changes the score as well.
            var answers = await db.AttemptAnswers.AsNoTracking().Where(entry => entry.AttemptId == id).ToListAsync(cancellationToken);
            var maximum = await db.QuizQuestions.AsNoTracking().Where(entry => entry.QuizId == quiz.Id).SumAsync(entry => entry.Points, cancellationToken);
            attempt.Grade(QuizRules.ScorePct(answers.Sum(entry => entry.Points ?? 0m), maximum));
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await grading.ScoreIfCompleteAsync(attempt, cancellationToken);
        }

        return await ViewAsync(attempt, quiz, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<AttemptSummaryDto>>> ListForQuizAsync(Guid quizId, CancellationToken cancellationToken)
    {
        var quiz = await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == quizId, cancellationToken);
        if (quiz is null || !(await scope.ForTeacherAsync(quiz.ClassId, cancellationToken)).IsSuccess)
        {
            return AppError.NotFound();
        }

        var attempts = await db.QuizAttempts.AsNoTracking().Where(attempt => attempt.QuizId == quizId)
            .OrderBy(attempt => attempt.StudentId).ThenBy(attempt => attempt.AttemptNo).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<AttemptSummaryDto>>(await SummariesAsync(attempts, cancellationToken));
    }

    public async Task<IReadOnlyList<StudentQuizDto>> MineAsync(CancellationToken cancellationToken)
    {
        var studentId = access.UserId;
        var classInfos = new List<ClassInfo>();
        foreach (var classId in await classes.GetStudentClassIdsAsync(studentId, cancellationToken))
        {
            if (await classes.GetClassInfoAsync(classId, cancellationToken) is { Status: not ClassStatus.Draft } info)
            {
                classInfos.Add(info);
            }
        }

        var classIds = classInfos.Select(info => info.Id).ToList();
        var quizzes = await db.Quizzes.AsNoTracking()
            .Where(quiz => classIds.Contains(quiz.ClassId) && quiz.Status == PublishStatus.Published)
            .OrderBy(quiz => quiz.CloseAt == null).ThenBy(quiz => quiz.CloseAt).ThenByDescending(quiz => quiz.CreatedAt)
            .ToListAsync(cancellationToken);
        var quizIds = quizzes.Select(quiz => quiz.Id).ToList();
        var counts = await db.QuizQuestions.AsNoTracking().Where(item => quizIds.Contains(item.QuizId))
            .GroupBy(item => item.QuizId).Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Key, row => row.Count, cancellationToken);
        var attempts = (await db.QuizAttempts.AsNoTracking()
                .Where(attempt => quizIds.Contains(attempt.QuizId) && attempt.StudentId == studentId)
                .OrderBy(attempt => attempt.AttemptNo)
                .ToListAsync(cancellationToken))
            .ToLookup(attempt => attempt.QuizId);
        var summaries = (await SummariesAsync([.. attempts.SelectMany(group => group)], cancellationToken)).ToLookup(summary => summary.Id);
        var now = timeProvider.GetUtcNow();
        var names = classInfos.ToDictionary(info => info.Id, info => info.Name);

        return
        [
            .. quizzes.Select(quiz =>
            {
                var mine = attempts[quiz.Id].ToList();
                return new StudentQuizDto(
                    quiz.Id, quiz.ClassId, names.GetValueOrDefault(quiz.ClassId, string.Empty), quiz.Kind, quiz.Title, quiz.TimeLimitMin,
                    quiz.MaxAttempts, quiz.OpenAt, quiz.CloseAt, counts.GetValueOrDefault(quiz.Id), mine.Count,
                    mine.FirstOrDefault(attempt => attempt.Status == AttemptStatus.InProgress)?.Id,
                    mine.Where(attempt => attempt.Status == AttemptStatus.Graded).Max(attempt => attempt.ScorePct),
                    QuizRules.IsOpen(quiz.OpenAt, quiz.CloseAt, now),
                    [.. mine.SelectMany(attempt => summaries[attempt.Id])]);
            }),
        ];
    }

    private async Task<IReadOnlyList<AttemptSummaryDto>> SummariesAsync(IReadOnlyList<QuizAttempt> attempts, CancellationToken cancellationToken)
    {
        var ids = attempts.Select(attempt => attempt.Id).ToList();
        var pending = await db.AttemptAnswers.AsNoTracking().Where(answer => ids.Contains(answer.AttemptId) && answer.Points == null)
            .GroupBy(answer => answer.AttemptId).Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Key, row => row.Count, cancellationToken);
        var profiles = await users.GetUsersAsync([.. attempts.Select(attempt => attempt.StudentId).Distinct()], cancellationToken);
        return
        [
            .. attempts.Select(attempt => new AttemptSummaryDto(
                attempt.Id, attempt.StudentId, profiles.TryGetValue(attempt.StudentId, out var profile) ? profile.DisplayName : string.Empty,
                attempt.AttemptNo, attempt.Status, attempt.StartedAt, attempt.SubmittedAt, attempt.ScorePct,
                attempt.Status == AttemptStatus.InProgress ? 0 : pending.GetValueOrDefault(attempt.Id))),
        ];
    }

    /// <summary>The attempt for its student or the class's teachers and admins; anyone else gets 404.</summary>
    private async Task<Result<(QuizAttempt Attempt, Quiz Quiz)>> VisibleAsync(Guid id, CancellationToken cancellationToken)
    {
        var attempt = await db.QuizAttempts.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (attempt is null)
        {
            return AppError.NotFound();
        }

        var quiz = await db.Quizzes.AsNoTracking().FirstAsync(item => item.Id == attempt.QuizId, cancellationToken);
        var allowed = access.IsStudent
            ? attempt.StudentId == access.UserId
            : (await scope.ForTeacherAsync(quiz.ClassId, cancellationToken)).IsSuccess;
        return allowed ? (attempt, quiz) : AppError.NotFound();
    }

    private async Task<Result<QuizAttempt>> OwnRunningAsync(Guid id, CancellationToken cancellationToken)
    {
        var attempt = await db.QuizAttempts.FirstOrDefaultAsync(item => item.Id == id && item.StudentId == access.UserId, cancellationToken);
        if (attempt is null)
        {
            return AppError.NotFound();
        }

        if (attempt.Status != AttemptStatus.InProgress || QuizRules.IsExpired(attempt.ExpiresAt, timeProvider.GetUtcNow(), grading.Grace))
        {
            if (attempt.Status == AttemptStatus.InProgress)
            {
                await grading.SubmitAsync(attempt, cancellationToken);
            }

            return new AppError(ErrorCodes.AttemptExpired, "The time for this attempt is over.");
        }

        return attempt;
    }

    private async Task<AttemptDto> ViewAsync(QuizAttempt attempt, Quiz quiz, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var items = await db.QuizQuestions.AsNoTracking().Where(item => item.QuizId == quiz.Id).ToDictionaryAsync(item => item.QuestionId, cancellationToken);
        var questions = await db.Questions.AsNoTracking().Where(question => attempt.QuestionOrder.Contains(question.Id)).ToDictionaryAsync(question => question.Id, cancellationToken);
        var answers = await db.AttemptAnswers.AsNoTracking().Where(answer => answer.AttemptId == attempt.Id).ToDictionaryAsync(answer => answer.QuestionId, cancellationToken);
        var used = await db.QuizAttempts.CountAsync(item => item.QuizId == quiz.Id && item.StudentId == attempt.StudentId, cancellationToken);

        var finished = attempt.Status != AttemptStatus.InProgress;
        // Staff always see everything; students see the key only when BR-KUIS-09 allows it.
        var review = finished && (!access.IsStudent || QuizRules.CanSeeReview(quiz.ShowReview, quiz.CloseAt, now, used, quiz.MaxAttempts));
        var list = attempt.QuestionOrder
            .Where(questions.ContainsKey)
            .Select(questionId =>
            {
                var question = questions[questionId];
                answers.TryGetValue(questionId, out var answer);
                return new AttemptQuestionDto(
                    SafeQuestions.For(question, SafeQuestions.SeededRandom(attempt.Id, questionId)),
                    items.TryGetValue(questionId, out var item) ? item.Points : 0m,
                    answer?.Answer,
                    review ? answer?.IsCorrect : null,
                    review ? answer?.Points : null,
                    review ? AnswerEvaluator.KeyOf(question.Kind, question.Payload) : null,
                    review ? question.Explanation : null);
            })
            .ToList();

        return new AttemptDto(
            attempt.Id, quiz.Id, quiz.Title, quiz.Kind, attempt.StudentId, attempt.AttemptNo, attempt.Status, attempt.StartedAt,
            attempt.ExpiresAt, attempt.SubmittedAt, attempt.ScorePct, review, now, list);
    }

    private static List<Guid> Shuffled(List<Guid> ids, Random random)
    {
        var list = new List<Guid>(ids);
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/quizzes/{id:guid}/attempts", async (Guid id, AttemptsHandler handler, CancellationToken cancellationToken) =>
                (await handler.StartAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.MuridOnly)
            .WithName("StartQuizAttempt")
            .WithSummary("Start or resume an attempt (murid of the class)")
            .Produces<AttemptDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/attempts/{id:guid}", async (Guid id, AttemptsHandler handler, CancellationToken cancellationToken) =>
                (await handler.GetAsync(id, cancellationToken)).ToOkResult())
            .WithName("GetQuizAttempt")
            .WithSummary("An attempt: student version while running, review per BR-KUIS-09 (owner, teachers, admin)")
            .Produces<AttemptDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/attempts/{id:guid}/answers/{questionId:guid}", async (Guid id, Guid questionId, SaveAttemptAnswerRequest request, AttemptsHandler handler, CancellationToken cancellationToken) =>
                (await handler.SaveAnswerAsync(id, questionId, request, cancellationToken)).ToHttpResult())
            .RequireAuthorization(Policies.MuridOnly)
            .WithName("SaveAttemptAnswer")
            .WithSummary("Autosave one answer (owner)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/attempts/{id:guid}/submit", async (Guid id, AttemptsHandler handler, CancellationToken cancellationToken) =>
                (await handler.SubmitAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.MuridOnly)
            .WithName("SubmitQuizAttempt")
            .WithSummary("Submit an attempt; answers are graded at once except long answers (owner)")
            .Produces<AttemptDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/attempts/{id:guid}/answers/{questionId:guid}/grade", async (Guid id, Guid questionId, GradeAnswerRequest request, AttemptsHandler handler, CancellationToken cancellationToken) =>
                (await handler.GradeAnswerAsync(id, questionId, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<GradeAnswerRequest>()
            .WithName("GradeAttemptAnswer")
            .WithSummary("Grade a long answer (teachers of the class, admin)")
            .Produces<AttemptDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/quizzes/{id:guid}/attempts", async (Guid id, AttemptsHandler handler, CancellationToken cancellationToken) =>
                (await handler.ListForQuizAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("ListQuizAttempts")
            .WithSummary("Attempts of a quiz with pending manual grading (teachers, admin)")
            .Produces<IReadOnlyList<AttemptSummaryDto>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/me/quizzes", async (AttemptsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.MineAsync(cancellationToken)))
            .RequireAuthorization(Policies.MuridOnly)
            .WithName("ListMyQuizzes")
            .WithSummary("Published quizzes and exams of my classes with my attempts (murid)")
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
