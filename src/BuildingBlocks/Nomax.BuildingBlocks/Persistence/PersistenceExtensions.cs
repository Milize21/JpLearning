using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nomax.BuildingBlocks.Messaging;
using Npgsql;

namespace Nomax.BuildingBlocks.Persistence;

public static class PersistenceExtensions
{
    public const string ConnectionStringName = "Postgres";

    /// <summary>EF migration history lives in each module's own schema (docs/03 §10).</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>
    /// One <see cref="NpgsqlDataSource"/> (and connection pool) shared by every module and the health check.
    /// The connection string is read when the data source is first needed, so configuration added later (tests) applies;
    /// a missing value still stops the host at startup.
    /// </summary>
    public static IServiceCollection AddNomaxPostgres(this IServiceCollection services)
    {
        services.AddRequiredConnectionString(ConnectionStringName, "In development set it with dotnet user-secrets (infra/README.md).");
        services.TryAddSingleton(provider => NpgsqlDataSource.Create(
            provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)!));
        return services;
    }

    /// <summary>Fails host startup when <c>ConnectionStrings:{name}</c> is empty.</summary>
    public static IServiceCollection AddRequiredConnectionString(this IServiceCollection services, string name, string hint)
    {
        services.AddOptions<RequiredConnectionString>(name)
            .Configure<IConfiguration>((options, configuration) => options.Value = configuration.GetConnectionString(name))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Value), $"ConnectionStrings:{name} is not configured. {hint}")
            .ValidateOnStart();
        return services;
    }

    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : ModuleDbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<TimestampsInterceptor>();
        services.AddSingleton(new ModuleDbContextRegistration(typeof(TContext)));
        services.AddDbContext<TContext>((provider, options) => options
            .UseNomaxPostgres(provider.GetRequiredService<NpgsqlDataSource>(), schema)
            .AddInterceptors(provider.GetRequiredService<TimestampsInterceptor>()));

        // Every module has an outbox in its schema and one processor sending it (docs/02 §6.3).
        services.AddOptions<MessagingOptions>().BindConfiguration(MessagingOptions.SectionName);
        services.TryAddSingleton<IntegrationEventDispatcher>();
        services.AddHostedService<OutboxProcessor<TContext>>();
        return services;
    }

    public sealed class RequiredConnectionString
    {
        public string? Value { get; set; }
    }

    public static DbContextOptionsBuilder UseNomaxPostgres(this DbContextOptionsBuilder options, NpgsqlDataSource dataSource, string schema)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options
            .UseNpgsql(dataSource, npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, schema))
            .UseSnakeCaseNamingConvention();
    }
}
