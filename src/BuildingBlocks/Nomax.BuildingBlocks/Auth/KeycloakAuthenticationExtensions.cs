using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Nomax.BuildingBlocks.Auth;

public static class KeycloakAuthenticationExtensions
{
    public static readonly PathString HubsPath = new("/hubs");

    /// <summary>
    /// JWT bearer validation against the Keycloak realm (issuer, audience <c>nomax-api</c>, signature, lifetime),
    /// used by both the Gateway and the API (docs/02 §8).
    /// </summary>
    public static IServiceCollection AddKeycloakJwtBearer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KeycloakOptions>()
            .Bind(configuration.GetSection(KeycloakOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<KeycloakOptions>>((options, keycloak) => Configure(options, keycloak.Value));

        return services;
    }

    private static void Configure(JwtBearerOptions options, KeycloakOptions keycloak)
    {
        options.Authority = keycloak.Authority;
        options.Audience = keycloak.Audience;
        options.RequireHttpsMetadata = keycloak.RequireHttpsMetadata;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = NomaxClaimTypes.PreferredUsername;
        options.TokenValidationParameters.RoleClaimType = NomaxClaimTypes.Role;
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ReadHubTokenFromQuery,
            OnTokenValidated = context =>
            {
                if (context.Principal is not null)
                {
                    RealmRoles.AddRoleClaims(context.Principal);
                }

                return Task.CompletedTask;
            },
        };
    }

    // Browsers cannot send headers on WebSocket requests, so SignalR passes the token as ?access_token=.
    // It is accepted only on /hubs/* so tokens never appear in other URLs or logs (docs/02 §8, docs/05 §3).
    private static Task ReadHubTokenFromQuery(MessageReceivedContext context)
    {
        var request = context.HttpContext.Request;
        if (request.Path.StartsWithSegments(HubsPath)
            && request.Query.TryGetValue("access_token", out var token)
            && !StringValues.IsNullOrEmpty(token))
        {
            context.Token = token.ToString();
        }

        return Task.CompletedTask;
    }
}
