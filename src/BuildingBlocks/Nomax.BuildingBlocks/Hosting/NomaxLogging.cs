using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Nomax.BuildingBlocks.Auth;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Nomax.BuildingBlocks.Hosting;

/// <summary>
/// Structured JSON logs on the console with TraceId, UserId (<c>sub</c>) and Module (docs/02 §15).
/// Never log tokens, passwords, file contents or email addresses.
/// </summary>
public static class NomaxLogging
{
    public static WebApplicationBuilder AddNomaxSerilog(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSerilog((services, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            // These categories log full request URLs, which can contain the ICS token or ?access_token=.
            .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning)
            .MinimumLevel.Override("Yarp", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .WriteTo.Console(new RenderedCompactJsonFormatter()));
        return builder;
    }

    public static IApplicationBuilder UseNomaxRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.GetMessageTemplateProperties = (context, path, elapsed, statusCode) =>
            [
                new LogEventProperty("RequestMethod", new ScalarValue(context.Request.Method)),
                new LogEventProperty("RequestPath", new ScalarValue(LogRedaction.RedactPath(path))),
                new LogEventProperty("StatusCode", new ScalarValue(statusCode)),
                new LogEventProperty("Elapsed", new ScalarValue(elapsed)),
            ];
            options.EnrichDiagnosticContext = (diagnostics, context) =>
            {
                if (context.User.FindUserId() is { } userId)
                {
                    diagnostics.Set("UserId", userId);
                }

                if (ModuleOf(context) is { } module)
                {
                    diagnostics.Set("Module", module);
                }
            };
        });

    /// <summary>Adds UserId and Module to every log event written while the request runs. Place after authentication.</summary>
    public static IApplicationBuilder UseNomaxLogContext(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            using var user = context.User.FindUserId() is { } userId ? LogContext.PushProperty("UserId", userId) : null;
            using var module = ModuleOf(context) is { } name ? LogContext.PushProperty("Module", name) : null;
            await next(context);
        });

    // Module endpoints carry their OpenAPI group name (see ModuleEndpoints.MapModuleGroup).
    private static string? ModuleOf(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<IEndpointGroupNameMetadata>()?.EndpointGroupName;
}
