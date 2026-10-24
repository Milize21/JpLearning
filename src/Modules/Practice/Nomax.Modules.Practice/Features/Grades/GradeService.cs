using Microsoft.EntityFrameworkCore;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Infrastructure;

namespace Nomax.Modules.Practice.Features.Grades;

/// <summary>
/// BR-NILAI-05: recomputes <c>class_grades</c> for a class (or some of its students) from published quizzes, exams and
/// assignments with the pure <see cref="GradeCalculator"/>. Callers: quiz and submission grading, scheme changes, the
/// hourly job and the grade pages.
/// </summary>
internal sealed class GradeService(PracticeDbContext db, IClassAccessReader classes, TimeProvider timeProvider)
{
    public async Task<GradeSettings> SettingsAsync(Guid classId, CancellationToken cancellationToken)
    {
        var scheme = await db.GradeSchemes.AsNoTracking().FirstOrDefaultAsync(item => item.ClassId == classId, cancellationToken);
        return scheme is null ? GradeSettings.Default : new GradeSettings(scheme.WQuiz, scheme.WExam, scheme.WAssignment, scheme.ScaleMap);
    }

    /// <summary>Recomputes and saves; <paramref name="studentIds"/> null = every active student of the class.</summary>
    public async Task RecomputeAsync(Guid classId, IReadOnlyCollection<Guid>? studentIds, CancellationToken cancellationToken)
    {
        var students = studentIds?.ToList() ?? [.. await classes.GetClassStudentIdsAsync(classId, cancellationToken)];
        if (students.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var settings = await SettingsAsync(classId, cancellationToken);
        var quizzes = await db.Quizzes.AsNoTracking()
            .Where(quiz => quiz.ClassId == classId && quiz.Status == PublishStatus.Published)
            .Select(quiz => new { quiz.Id, quiz.Kind, quiz.CloseAt })
            .ToListAsync(cancellationToken);
        var quizIds = quizzes.Select(quiz => quiz.Id).ToList();
        var best = (await db.QuizAttempts.AsNoTracking()
                .Where(attempt => quizIds.Contains(attempt.QuizId) && students.Contains(attempt.StudentId) && attempt.Status == AttemptStatus.Graded)
                .GroupBy(attempt => new { attempt.QuizId, attempt.StudentId })
                .Select(group => new { group.Key.QuizId, group.Key.StudentId, Best = group.Max(attempt => attempt.ScorePct) })
                .ToListAsync(cancellationToken))
            .ToDictionary(row => (row.QuizId, row.StudentId), row => row.Best);
        var assignments = await db.Assignments.AsNoTracking()
            .Where(item => item.ClassId == classId && item.Status == PublishStatus.Published)
            .Select(item => new { item.Id, item.DueAt })
            .ToListAsync(cancellationToken);
        var assignmentIds = assignments.Select(item => item.Id).ToList();
        var submissions = (await db.Submissions.AsNoTracking()
                .Where(item => assignmentIds.Contains(item.AssignmentId) && students.Contains(item.StudentId))
                .Select(item => new { item.AssignmentId, item.StudentId, item.GradePct })
                .ToListAsync(cancellationToken))
            .ToDictionary(row => (row.AssignmentId, row.StudentId), row => row.GradePct);
        var existing = await db.ClassGrades.Where(grade => grade.ClassId == classId && students.Contains(grade.StudentId))
            .ToDictionaryAsync(grade => grade.StudentId, cancellationToken);

        foreach (var studentId in students)
        {
            var quizInputs = quizzes.Select(quiz => new QuizResultInput(quiz.Kind, quiz.CloseAt, best.GetValueOrDefault((quiz.Id, studentId)))).ToList();
            var assignmentInputs = assignments.Select(item =>
            {
                var submitted = submissions.TryGetValue((item.Id, studentId), out var grade);
                return new AssignmentResultInput(item.DueAt, submitted, grade);
            }).ToList();
            var result = GradeCalculator.Compute(quizInputs, assignmentInputs, settings, now);

            if (!existing.TryGetValue(studentId, out var row))
            {
                row = ClassGrade.Create(classId, studentId);
                db.ClassGrades.Add(row);
            }

            row.Set(result, now);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
