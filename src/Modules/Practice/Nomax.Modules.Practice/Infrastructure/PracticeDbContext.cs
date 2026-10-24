using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Practice.Domain;

namespace Nomax.Modules.Practice.Infrastructure;

public sealed class PracticeDbContext(DbContextOptions<PracticeDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "practice";

    public DbSet<QuestionSource> QuestionSources => Set<QuestionSource>();

    public DbSet<Question> Questions => Set<Question>();

    public DbSet<DrillSession> DrillSessions => Set<DrillSession>();

    public DbSet<DrillAnswer> DrillAnswers => Set<DrillAnswer>();

    public DbSet<ItemMastery> ItemMasteries => Set<ItemMastery>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<Quiz> Quizzes => Set<Quiz>();

    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();

    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();

    public DbSet<AttemptAnswer> AttemptAnswers => Set<AttemptAnswer>();

    public DbSet<Assignment> Assignments => Set<Assignment>();

    public DbSet<Submission> Submissions => Set<Submission>();

    public DbSet<GradeScheme> GradeSchemes => Set<GradeScheme>();

    public DbSet<ClassGrade> ClassGrades => Set<ClassGrade>();

    protected override string Schema => SchemaName;
}
