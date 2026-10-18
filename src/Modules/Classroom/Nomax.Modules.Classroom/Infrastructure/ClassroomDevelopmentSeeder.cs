using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Classroom.Domain;

namespace Nomax.Modules.Classroom.Infrastructure;

/// <summary>
/// "Kelas Demo Dasar" of docs/03 §13: sensei.dev as primary teacher, murid1.dev and murid2.dev as students, chapter 1 of the
/// kana book unlocked. The level/book/chapter ids are the fixed development ids of the Content seed; user ids are the
/// realm export ids. Idempotent.
/// </summary>
internal sealed class ClassroomDevelopmentSeeder(ClassroomDbContext db, TimeProvider timeProvider) : IDevelopmentSeeder
{
    public static readonly Guid DemoClass = Guid.Parse("c0000000-0000-4000-8000-000000000001");

    private static readonly Guid LevelDasar = Guid.Parse("b0000000-0000-4000-8000-000000000001");
    private static readonly Guid BookKana = Guid.Parse("b1000000-0000-4000-8000-000000000001");
    private static readonly Guid Chapter1 = Guid.Parse("b2000000-0000-4000-8000-000000000001");
    private static readonly Guid Sensei = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Murid1 = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid Murid2 = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.Classes.AnyAsync(item => item.Id == DemoClass, cancellationToken))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        db.Classes.Add(StudyClass.Create(DemoClass, LevelDasar, BookKana, "Kelas Demo Dasar", ClassStatus.Active, Sensei));
        db.ClassTeachers.Add(ClassTeacher.Create(DemoClass, Sensei, isPrimary: true));
        db.ClassStudents.Add(ClassStudent.Join(DemoClass, Murid1, now));
        db.ClassStudents.Add(ClassStudent.Join(DemoClass, Murid2, now));
        db.ClassUnlocks.Add(ClassUnlock.Create(DemoClass, Chapter1, Sensei, now));
        await db.SaveChangesAsync(cancellationToken);
    }
}
