using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Nomax.BuildingBlocks.Persistence;

/// <summary>
/// Development data of one module (docs/03 §13). Runs only in the Development environment and must be idempotent.
/// </summary>
public interface IDevelopmentSeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}

/// <summary>Configuration section <c>Database</c>.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Apply pending migrations at startup. True in development; production runs a migration bundle (docs/02 §7).</summary>
    public bool MigrateOnStartup { get; set; }
}

internal sealed record ModuleDbContextRegistration(Type ContextType);

public static class DatabaseInitialization
{
    /// <summary>Migrates every module DbContext (when enabled) and then runs the development seeders.</summary>
    public static async Task InitializeNomaxDatabasesAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        var options = app.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();

        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        if (options.MigrateOnStartup)
        {
            foreach (var registration in services.GetServices<ModuleDbContextRegistration>())
            {
                var context = (DbContext)services.GetRequiredService(registration.ContextType);
                await context.Database.MigrateAsync(cancellationToken);
            }
        }

        if (app.Environment.IsDevelopment())
        {
            foreach (var seeder in services.GetServices<IDevelopmentSeeder>())
            {
                await seeder.SeedAsync(cancellationToken);
            }
        }
    }
}
