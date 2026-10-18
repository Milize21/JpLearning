using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Identity.Domain;

namespace Nomax.Modules.Identity.Infrastructure;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "identity";

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override string Schema => SchemaName;
}
