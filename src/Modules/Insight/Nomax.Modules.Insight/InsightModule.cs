using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nomax.BuildingBlocks.Hosting;
using Nomax.BuildingBlocks.Messaging;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Identity.Contracts;
using Nomax.Modules.Insight.Features;
using Nomax.Modules.Insight.Infrastructure;
using Nomax.Modules.Insight.IntegrationEvents;
using Nomax.Modules.Insight.Leaderboard;
using Nomax.Modules.Practice.Contracts;
using Nomax.SharedKernel.Serialization;
using StackExchange.Redis;

namespace Nomax.Modules.Insight;

/// <summary>
/// Insight module (docs/02 §3): activity log, points (BR-POIN), seasons (BR-SEASON) and the live leaderboard over Redis
/// sorted sets and the SignalR hub <c>/hubs/leaderboard</c>.
/// </summary>
public static class InsightModule
{
    public const string Name = "insight";

    public static IServiceCollection AddInsightModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddNomaxModule(Name);
        services.AddModuleDbContext<InsightDbContext>(InsightDbContext.SchemaName);
        services.AddOptions<LeaderboardOptions>().Bind(configuration.GetSection(LeaderboardOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();

        // Several API instances share hub groups through the Redis backplane (docs/02 §7). The connection string is read
        // when the hub starts, so tests can replace it.
        services.AddSignalR()
            .AddJsonProtocol(options => options.PayloadSerializerOptions = new(NomaxJson.Options))
            .AddStackExchangeRedis();
        services.AddOptions<RedisOptions>().Configure<IConfiguration>((options, config) =>
        {
            options.Configuration = ConfigurationOptions.Parse(config.GetConnectionString(NomaxApiDefaults.RedisConnectionStringName) ?? string.Empty);
            options.Configuration.AbortOnConnectFail = false;
            options.Configuration.ChannelPrefix = RedisChannel.Literal("nomax-hub");
        });
        services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();

        services.AddSingleton<EntryKeys>();
        services.AddSingleton<LeaderboardBroadcaster>();
        services.AddHostedService(provider => provider.GetRequiredService<LeaderboardBroadcaster>());
        services.AddScoped<SeasonService>();
        services.AddScoped<LeaderboardStore>();
        services.AddScoped<LeaderboardQueries>();
        services.AddScoped<LeaderboardNotifier>();
        services.AddScoped<PointLedger>();
        services.AddScoped<IDevelopmentSeeder, InsightDevelopmentSeeder>();

        services.AddIntegrationEventConsumer<AnswerSubmitted, AnswerSubmittedConsumer>();
        services.AddIntegrationEventConsumer<QuizGraded, QuizGradedConsumer>();
        services.AddIntegrationEventConsumer<DrillSessionEnded, DrillSessionEndedConsumer>();
        services.AddIntegrationEventConsumer<SubmissionGraded, SubmissionGradedConsumer>();
        services.AddIntegrationEventConsumer<ChapterUnlocked, ChapterUnlockedConsumer>();
        services.AddIntegrationEventConsumer<ChapterLocked, ChapterLockedConsumer>();
        services.AddIntegrationEventConsumer<UserAccountCreated, UserAccountCreatedConsumer>();
        services.AddIntegrationEventConsumer<UserDeactivated, UserDeactivatedConsumer>();
        services.AddIntegrationEventConsumer<UserReactivated, UserReactivatedConsumer>();
        services.AddIntegrationEventConsumer<UserDisplayNameChanged, UserDisplayNameChangedConsumer>();

        services.AddScoped<LeaderboardsHandler>();
        services.AddScoped<ActivityHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapInsightEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapModuleGroup(Name);
        LeaderboardsHandler.Map(group);
        ActivityHandler.Map(group);
        endpoints.MapHub<LeaderboardHub>(LeaderboardHub.Path);
        return endpoints;
    }
}
