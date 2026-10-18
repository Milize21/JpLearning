using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Hosting;
using Nomax.BuildingBlocks.Persistence;
using Nomax.Modules.Identity.Contracts;
using Nomax.Modules.Identity.Features.Common;
using Nomax.Modules.Identity.Features.MyProfile;
using Nomax.Modules.Identity.Features.Users;
using Nomax.Modules.Identity.Infrastructure;
using Nomax.Modules.Identity.Infrastructure.Keycloak;
using Nomax.Modules.Identity.Public;

namespace Nomax.Modules.Identity;

/// <summary>
/// Identity module (docs/02 §3): profiles and preferences, account management through the Keycloak Admin API.
/// </summary>
public static class IdentityModule
{
    public const string Name = "identity";

    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddNomaxModule(Name);
        services.AddModuleDbContext<IdentityDbContext>(IdentityDbContext.SchemaName);
        services.AddValidatorsFromAssemblyContaining<CreateUserValidator>(ServiceLifetime.Singleton, includeInternalTypes: true);

        services.AddOptions<KeycloakAdminOptions>()
            .Bind(configuration.GetSection(KeycloakOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddHttpClient(KeycloakAdminTokenProvider.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<KeycloakAdminTokenProvider>();
        services.AddHttpClient<IKeycloakAdmin, KeycloakAdminClient>(client => client.Timeout = TimeSpan.FromSeconds(10));

        services.AddSingleton<AccountStatusCache>();
        services.AddScoped<IAccountStatusReader, AccountStatusReader>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IDevelopmentSeeder, IdentityDevelopmentSeeder>();

        services.AddScoped<AccountProvisioner>();
        services.AddScoped<GetMeHandler>();
        services.AddScoped<UpdatePreferencesHandler>();
        services.AddScoped<ListUsersHandler>();
        services.AddScoped<GetUserHandler>();
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<UpdateUserHandler>();
        services.AddScoped<ImportUsersHandler>();
        services.AddScoped<ResetPasswordHandler>();
        services.AddScoped<SetAccountActiveHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapModuleGroup(Name);
        GetMeHandler.Map(group);
        UpdatePreferencesHandler.Map(group);
        ListUsersHandler.Map(group);
        GetUserHandler.Map(group);
        CreateUserHandler.Map(group);
        UpdateUserHandler.Map(group);
        ImportUsersHandler.Map(group);
        ResetPasswordHandler.Map(group);
        SetAccountActiveHandler.Map(group);
        return endpoints;
    }
}
