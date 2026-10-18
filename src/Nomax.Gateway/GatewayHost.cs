using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Hosting;

namespace Nomax.Gateway;

/// <summary>
/// Gateway composition (docs/02 §3, §8): validates the JWT a first time, applies rate limits and security headers,
/// then forwards <c>/api</c>, <c>/hubs</c>, <c>/ical</c> (and in development <c>/openapi</c>, <c>/scalar</c>) to Nomax.Api.
/// </summary>
public static class GatewayHost
{
    public static WebApplicationBuilder AddNomaxGateway(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;

        builder.AddNomaxSerilog();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = NomaxApiDefaults.MaxRequestBodyBytes);

        services.AddNomaxProblemDetails();
        services.AddKeycloakJwtBearer(builder.Configuration);
        services.AddNomaxAuthorization();
        services.AddGatewayRateLimiting(builder.Configuration);
        services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

        services.AddHttpClient(ApiHealthCheck.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddHealthChecks().AddCheck<ApiHealthCheck>("api", tags: [HealthEndpoints.ReadyTag]);

        return builder;
    }

    public static WebApplication UseNomaxGateway(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseSecurityHeaders();
        app.UseUploadBodyLimit();
        app.UseNomaxRequestLogging();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        // Before authorization, so anonymous floods are limited per IP before they are rejected with 401.
        app.UseRateLimiter();
        app.UseAuthorization();

        app.MapNomaxHealthChecks();
        app.MapReverseProxy();
        return app;
    }
}
