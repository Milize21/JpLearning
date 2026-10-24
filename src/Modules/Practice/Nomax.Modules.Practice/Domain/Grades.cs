using System.Text.Json.Serialization;
using Nomax.BuildingBlocks.Persistence;

namespace Nomax.Modules.Practice.Domain;

/// <summary>One row of <c>scale_map</c>: from <see cref="Min"/> percent the grade is <see cref="Gpa"/>.</summary>
public sealed record ScaleStep(decimal Min, decimal Gpa);

/// <summary>practice.grade_schemes (docs/03 §7.12).</summary>
public sealed class GradeScheme : IHasTimestamps, IHasVersion
{
    private GradeScheme()
    {
    }

    public Guid ClassId { get; private set; }

    public decimal WQuiz { get; private set; }

    public decimal WExam { get; private set; }

    public decimal WAssignment { get; private set; }

    public List<ScaleStep> ScaleMap { get; private set; } = [];

    public uint Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static GradeScheme Create(Guid classId, decimal wQuiz, decimal wExam, decimal wAssignment, IEnumerable<ScaleStep> scaleMap) =>
        new() { ClassId = classId, WQuiz = wQuiz, WExam = wExam, WAssignment = wAssignment, ScaleMap = [.. scaleMap] };

    public void Update(decimal wQuiz, decimal wExam, decimal wAssignment, IEnumerable<ScaleStep> scaleMap)
    {
        WQuiz = wQuiz;
        WExam = wExam;
        WAssignment = wAssignment;
        ScaleMap = [.. scaleMap];
    }
}

/// <summary><c>class_grades.breakdown</c> (docs/03 §7.13).</summary>
public sealed record GradeBreakdown(
    decimal? QuizAvg,
    decimal? ExamAvg,
    decimal? AssignmentAvg,
    [property: JsonPropertyName("weightsUsed")] IReadOnlyDictionary<string, decimal> WeightsUsed);

/// <summary>practice.class_grades (docs/03 §7.13), recomputed by BR-NILAI-05.</summary>
public sealed class ClassGrade : IHasTimestamps
{
    private ClassGrade()
    {
    }

    public Guid ClassId { get; private set; }

    public Guid StudentId { get; private set; }

    public decimal? FinalPct { get; private set; }

    public decimal? Gpa04 { get; private set; }

    public DateTimeOffset ComputedAt { get; private set; }

    public GradeBreakdown Breakdown { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ClassGrade Create(Guid classId, Guid studentId) => new() { ClassId = classId, StudentId = studentId };

    public void Set(GradeResult result, DateTimeOffset at)
    {
        FinalPct = result.FinalPct;
        Gpa04 = result.Gpa;
        Breakdown = result.Breakdown;
        ComputedAt = at;
    }
}

/// <summary>Weights in percent and the conversion table of a class (BR-NILAI-06).</summary>
public sealed record GradeSettings(decimal WQuiz, decimal WExam, decimal WAssignment, IReadOnlyList<ScaleStep> ScaleMap)
{
    // Declared first: static initializers run in source order and Default needs it.
    public static IReadOnlyList<ScaleStep> DefaultScale { get; } =
        [new(80m, 4m), new(70m, 3m), new(60m, 2m), new(50m, 1m), new(0m, 0m)];

    /// <summary>D-16 defaults: 30/40/30 and ≥80 = 4, 70 = 3, 60 = 2, 50 = 1, else 0.</summary>
    public static GradeSettings Default { get; } = new(30m, 40m, 30m, DefaultScale);
}

/// <summary>A published quiz or exam for one student: its best graded score, if any.</summary>
public sealed record QuizResultInput(QuizKind Kind, DateTimeOffset? CloseAt, decimal? BestGradedScore);

/// <summary>A published assignment for one student.</summary>
public sealed record AssignmentResultInput(DateTimeOffset DueAt, bool Submitted, decimal? GradePct);

public sealed record GradeResult(decimal? FinalPct, decimal? Gpa, GradeBreakdown Breakdown);

/// <summary>BR-NILAI-01…04 and BR-NILAI-06 as pure functions.</summary>
public static class GradeCalculator
{
    /// <summary>BR-NILAI-01 for quizzes or exams: best graded score; nothing after the close time counts as 0; otherwise skipped.</summary>
    public static decimal? QuizAverage(IEnumerable<QuizResultInput> quizzes, DateTimeOffset now) =>
        Average(quizzes.Select(quiz => quiz.BestGradedScore ?? (quiz.CloseAt is { } close && close <= now ? 0m : (decimal?)null)));

    /// <summary>BR-NILAI-01 for assignments: the grade; not submitted after the due time = 0; submitted but ungraded or not due = skipped.</summary>
    public static decimal? AssignmentAverage(IEnumerable<AssignmentResultInput> assignments, DateTimeOffset now) =>
        Average(assignments.Select(item => item.GradePct ?? (!item.Submitted && item.DueAt <= now ? 0m : (decimal?)null)));

    /// <summary>BR-NILAI-02/03/04: weighted over the components that have at least one counted item, then the 0–4 grade.</summary>
    public static GradeResult Compute(
        IReadOnlyCollection<QuizResultInput> quizzes, IReadOnlyCollection<AssignmentResultInput> assignments, GradeSettings settings, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var quizAvg = QuizAverage(quizzes.Where(quiz => quiz.Kind == QuizKind.Quiz), now);
        var examAvg = QuizAverage(quizzes.Where(quiz => quiz.Kind == QuizKind.Exam), now);
        var assignmentAvg = AssignmentAverage(assignments, now);

        var weights = new Dictionary<string, decimal>();
        decimal weighted = 0m;
        void Add(string name, decimal? average, decimal weight)
        {
            if (average is { } value)
            {
                weights[name] = weight;
                weighted += weight * value;
            }
        }

        Add("quiz", quizAvg, settings.WQuiz);
        Add("exam", examAvg, settings.WExam);
        Add("assignment", assignmentAvg, settings.WAssignment);

        var totalWeight = weights.Values.Sum();
        decimal? final = weights.Count == 0 || totalWeight == 0 ? null : Round(weighted / totalWeight);
        return new GradeResult(final, final is { } pct ? ToGpa(pct, settings.ScaleMap) : null, new GradeBreakdown(quizAvg, examAvg, assignmentAvg, weights));
    }

    /// <summary>BR-NILAI-03: the first step from the highest <c>min</c> that the rounded percentage reaches.</summary>
    public static decimal ToGpa(decimal finalPct, IReadOnlyList<ScaleStep> scale) =>
        scale.OrderByDescending(step => step.Min).FirstOrDefault(step => finalPct >= step.Min)?.Gpa ?? 0m;

    /// <summary>BR-NILAI-06: weights ≥ 0 summing to 100; unique mins including 0; gpa within 0–4. Returns problems.</summary>
    public static IReadOnlyList<string> Validate(decimal wQuiz, decimal wExam, decimal wAssignment, IReadOnlyList<ScaleStep> scale)
    {
        ArgumentNullException.ThrowIfNull(scale);
        var problems = new List<string>();
        if (wQuiz < 0 || wExam < 0 || wAssignment < 0 || wQuiz + wExam + wAssignment != 100m)
        {
            problems.Add("weights");
        }

        if (scale.Count == 0 || scale.Select(step => step.Min).Distinct().Count() != scale.Count || scale.All(step => step.Min != 0m)
            || scale.Any(step => step.Min < 0 || step.Min > 100))
        {
            problems.Add("scaleMap.min");
        }

        if (scale.Any(step => step.Gpa < 0 || step.Gpa > 4))
        {
            problems.Add("scaleMap.gpa");
        }

        return problems;
    }

    /// <summary>BR-NILAI-04: two decimals, away from zero.</summary>
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal? Average(IEnumerable<decimal?> values)
    {
        var counted = values.OfType<decimal>().ToList();
        return counted.Count == 0 ? null : Round(counted.Average());
    }
}
