using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Generator;
using Nomax.Modules.Practice.Features.Questions;

namespace Nomax.Modules.Practice.Infrastructure;

/// <summary>
/// docs/03 §13: the source "Internal NomaX" (license internal), and published kana questions generated from the seeded kana
/// of the Dasar level so drilling works right after setup. Idempotent: the generator skips template + item pairs that exist.
/// </summary>
internal sealed class PracticeDevelopmentSeeder(PracticeDbContext db, QuestionStore store, QuestionGeneratorService generator) : IDevelopmentSeeder
{
    private static readonly Guid LevelDasar = Guid.Parse("b0000000-0000-4000-8000-000000000001");
    private static readonly Guid Admin = Guid.Parse("a0000000-0000-4000-8000-000000000001");

    private static readonly GeneratorTemplate[] KanaTemplates =
        [GeneratorTemplate.KanaToRomaji, GeneratorTemplate.RomajiToKana, GeneratorTemplate.KanaTypeRomaji];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await store.InternalSourceIdAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await generator.RunAsync(LevelDasar, KanaTemplates, [], Admin, QuestionStatus.Published, cancellationToken);
    }
}
