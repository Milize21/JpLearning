using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Insight.Domain;

namespace Nomax.Modules.Insight.Infrastructure;

public sealed class InsightDbContext(DbContextOptions<InsightDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "insight";

    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    public DbSet<LeaderboardSeason> Seasons => Set<LeaderboardSeason>();

    public DbSet<PointEvent> PointEvents => Set<PointEvent>();

    public DbSet<SeasonStanding> SeasonStandings => Set<SeasonStanding>();

    protected override string Schema => SchemaName;
}
