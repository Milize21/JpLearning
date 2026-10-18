using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Nomax.BuildingBlocks.Messaging;

namespace Nomax.BuildingBlocks.Persistence;

/// <summary>
/// Base class for the single DbContext of each module (docs/02 §5, §7): its own schema, configurations from the
/// module assembly, snake_case enums with CHECK constraints and timestamp defaults.
/// A module never uses another module's DbContext (AGENTS.md §4).
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Database schema of the module, e.g. <c>identity</c>.</summary>
    protected abstract string Schema { get; }

    /// <summary>Integration events waiting to be sent (docs/03 §10); filled through <c>AddIntegrationEvent</c>.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>Integration events this module has already handled (docs/03 §10).</summary>
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        MessagingModel.Configure(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        // docs/03 lists every index; EF must not add one per foreign key on its own.
        configurationBuilder.Conventions.Remove<ForeignKeyIndexConvention>();
        configurationBuilder.Conventions.Add(_ => new SnakeCaseEnumConvention());
        configurationBuilder.Conventions.Add(_ => new TimestampsConvention());
        base.ConfigureConventions(configurationBuilder);
    }
}
