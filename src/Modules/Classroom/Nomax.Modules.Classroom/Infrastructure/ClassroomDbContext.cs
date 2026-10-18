using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Classroom.Domain;

namespace Nomax.Modules.Classroom.Infrastructure;

public sealed class ClassroomDbContext(DbContextOptions<ClassroomDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "classroom";

    public DbSet<StudyClass> Classes => Set<StudyClass>();

    public DbSet<ClassTeacher> ClassTeachers => Set<ClassTeacher>();

    public DbSet<ClassStudent> ClassStudents => Set<ClassStudent>();

    public DbSet<ClassUnlock> ClassUnlocks => Set<ClassUnlock>();

    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();

    public DbSet<CalendarFeedToken> CalendarFeedTokens => Set<CalendarFeedToken>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    protected override string Schema => SchemaName;
}
