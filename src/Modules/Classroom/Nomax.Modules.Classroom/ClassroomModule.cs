using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nomax.BuildingBlocks.Hosting;
using Nomax.BuildingBlocks.Messaging;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Classroom.Features.Calendar;
using Nomax.Modules.Classroom.Features.Classes;
using Nomax.Modules.Classroom.Features.Common;
using Nomax.Modules.Classroom.Features.Members;
using Nomax.Modules.Classroom.Features.Push;
using Nomax.Modules.Classroom.Features.Sessions;
using Nomax.Modules.Classroom.Features.Unlocks;
using Nomax.Modules.Classroom.Infrastructure;
using Nomax.Modules.Classroom.Infrastructure.Notifications;
using Nomax.Modules.Classroom.IntegrationEvents;
using Nomax.Modules.Classroom.Public;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Identity.Contracts;

namespace Nomax.Modules.Classroom;

/// <summary>
/// Classroom module (docs/02 §3): classes, members, chapter unlocks, schedule, ICS feed, notifications and web push.
/// </summary>
public static class ClassroomModule
{
    public const string Name = "classroom";

    public static IServiceCollection AddClassroomModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddNomaxModule(Name);
        services.AddModuleDbContext<ClassroomDbContext>(ClassroomDbContext.SchemaName);
        services.AddValidatorsFromAssemblyContaining<CreateClassValidator>(ServiceLifetime.Singleton, includeInternalTypes: true);

        services.AddOptions<ScheduleOptions>().Bind(configuration.GetSection(ScheduleOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AppOptions>().Bind(configuration.GetSection(AppOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<EmailOptions>().Bind(configuration.GetSection(EmailOptions.SectionName)).ValidateDataAnnotations();
        services.AddOptions<WebPushOptions>().Bind(configuration.GetSection(WebPushOptions.SectionName));
        services.AddScoped<NotificationPlanner>();
        services.AddScoped<EmailSender>();
        services.AddHttpClient<WebPushSender>(client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddHostedService<NotificationDispatcher>();
        services.AddIntegrationEventConsumer<UserDeactivated, UserDeactivatedConsumer>();

        services.AddScoped<IClassAccessReader, ClassAccessReader>();
        services.AddScoped<IContentUsageProbe, ClassroomContentUsage>();
        services.AddScoped<IDevelopmentSeeder, ClassroomDevelopmentSeeder>();

        services.AddScoped<ClassAccess>();
        services.AddScoped<ClassViews>();

        services.AddScoped<ListClassesHandler>();
        services.AddScoped<CreateClassHandler>();
        services.AddScoped<GetClassHandler>();
        services.AddScoped<UpdateClassHandler>();
        services.AddScoped<ArchiveClassHandler>();
        services.AddScoped<StudentsHandler>();
        services.AddScoped<TeachersHandler>();
        services.AddScoped<UnlocksHandler>();
        services.AddScoped<ListSessionsHandler>();
        services.AddScoped<CreateSessionHandler>();
        services.AddScoped<UpdateSessionHandler>();
        services.AddScoped<CancelSessionHandler>();
        services.AddScoped<CalendarFeedHandler>();
        services.AddScoped<PushSubscriptionsHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapClassroomEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapModuleGroup(Name);
        ListClassesHandler.Map(group);
        CreateClassHandler.Map(group);
        GetClassHandler.Map(group);
        UpdateClassHandler.Map(group);
        ArchiveClassHandler.Map(group);
        StudentsHandler.Map(group);
        TeachersHandler.Map(group);
        UnlocksHandler.Map(group);
        ListSessionsHandler.Map(group);
        CreateSessionHandler.Map(group);
        UpdateSessionHandler.Map(group);
        CancelSessionHandler.Map(group);
        CalendarFeedHandler.Map(group, endpoints);
        PushSubscriptionsHandler.Map(group);
        return endpoints;
    }
}
