using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Content.Contracts;

namespace Nomax.Modules.Insight.Infrastructure;

/// <summary>BR-SEASON-01 / docs/03 §13: an active "{level} · Musim 1" for every seeded level. Idempotent.</summary>
internal sealed class InsightDevelopmentSeeder(IContentCatalog catalog, SeasonService seasons) : IDevelopmentSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (var level in await catalog.GetLevelsAsync(cancellationToken))
        {
            await seasons.ActiveAsync(level.Id, cancellationToken);
        }
    }
}
