using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nomax.BuildingBlocks.Messaging;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Practice.Contracts;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Grades;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.SharedKernel.Serialization;

namespace Nomax.Modules.Practice.Features.Quizzes;

/// <summary>
/// Submitting and scoring attempts (BR-KUIS-07/08): every answer is graded automatically except long answers; once
/// nothing waits for a teacher the score is set, <c>QuizGraded</c> is queued and the class grade recomputed (BR-NILAI-05).
/// </summary>
internal sealed class AttemptGrading(
    PracticeDbContext db, IClassAccessReader classes, GradeService grades, IOptions<QuizOptions> options, TimeProvider timeProvider)
{
    public TimeSpan Grace => TimeSpan.FromSeconds(options.Value.GraceSeconds);

    /// <summary>Submits an attempt in progress (by the student or the expiry job) and scores what can be scored.</summary>
    public async Task SubmitAsync(QuizAttempt attempt, CancellationToken cancellationToken)
    {
        if (attempt.Status != AttemptStatus.InProgress)
        {
            return;
        }

        // An expired attempt counts as submitted at its expiry time, never later.
        var now = timeProvider.GetUtcNow();
        attempt.Submit(attempt.ExpiresAt is { } expires && expires < now ? expires : now);

        var items = await db.QuizQuestions.AsNoTracking().Where(item => item.QuizId == attempt.QuizId).ToDictionaryAsync(item => item.QuestionId, cancellationToken);
        var questions = await db.Questions.AsNoTracking().Where(question => attempt.QuestionOrder.Contains(question.Id)).ToDictionaryAsync(question => question.Id, cancellationToken);
        var answers = await db.AttemptAnswers.Where(answer => answer.AttemptId == attempt.Id).ToDictionaryAsync(answer => answer.QuestionId, cancellationToken);

        foreach (var questionId in attempt.QuestionOrder)
        {
            if (!questions.TryGetValue(questionId, out var question) || !items.TryGetValue(questionId, out var item))
            {
                continue;
            }

            if (!answers.TryGetValue(questionId, out var answer))
            {
                answer = AttemptAnswer.Create(attempt.Id, questionId, null);
                db.AttemptAnswers.Add(answer);
                answers[questionId] = answer;
            }

            var grade = QuizRules.Grade(question.Kind, question.Payload, answer.Answer, item.Points);
            answer.SetResult(grade.IsCorrect, grade.Points);
        }

        await db.SaveChangesAsync(cancellationToken);
        await ScoreIfCompleteAsync(attempt, cancellationToken);
    }

    /// <summary>BR-KUIS-08: once every answer has points, set the score, publish <c>QuizGraded</c> and recompute grades.</summary>
    public async Task ScoreIfCompleteAsync(QuizAttempt attempt, CancellationToken cancellationToken)
    {
        if (attempt.Status != AttemptStatus.Submitted)
        {
            return;
        }

        var answers = await db.AttemptAnswers.AsNoTracking().Where(answer => answer.AttemptId == attempt.Id).ToListAsync(cancellationToken);
        if (answers.Any(answer => answer.Points is null))
        {
            return;
        }

        var maximum = await db.QuizQuestions.AsNoTracking().Where(item => item.QuizId == attempt.QuizId).SumAsync(item => item.Points, cancellationToken);
        var score = QuizRules.ScorePct(answers.Sum(answer => answer.Points ?? 0m), maximum);
        attempt.Grade(score);

        var quiz = await db.Quizzes.AsNoTracking().FirstAsync(item => item.Id == attempt.QuizId, cancellationToken);
        var info = await classes.GetClassInfoAsync(quiz.ClassId, cancellationToken);
        db.AddIntegrationEvent(
            new QuizGraded(attempt.Id, quiz.Id, EnumSnakeCase.ToValue(quiz.Kind), quiz.ClassId, info?.LevelId ?? Guid.Empty,
                attempt.StudentId, score, attempt.AttemptNo, timeProvider.GetUtcNow()),
            timeProvider);
        await db.SaveChangesAsync(cancellationToken);
        await grades.RecomputeAsync(quiz.ClassId, [attempt.StudentId], cancellationToken);
    }
}
