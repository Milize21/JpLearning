using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.SharedKernel.Results;

namespace Nomax.Gateway;

/// <summary>
/// Rate limits from docs/05 §1. The drill-answer (120/min) and upload (20/hour) limits are added with their routes in Phases 1–2.
/// </summary>
public sealed class GatewayRateLimitOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, int.MaxValue)]
    public int PerUserPerMinute { get; set; } = 300;

    [Range(1, int.MaxValue)]
    public int IcalPerIpPerMinute { get; set; } = 60;
}

public static class GatewayRateLimiting
{
    /// <summary>Per signed-in user (<c>sub</c>), or per IP for anonymous requests.</summary>
    public const string PerUserPolicy = "per-user";

    /// <summary>Public ICS feed, per IP.</summary>
    public const string IcalPolicy = "ical";

    public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GatewayRateLimitOptions>()
            .Bind(configuration.GetSection(GatewayRateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = OnRejectedAsync;
            options.AddPolicy(PerUserPolicy, context =>
            {
                var limits = Limits(context);
                var key = context.User.FindUserId() is { } userId ? $"user:{userId}" : $"ip:{ClientIp(context)}";
                return PerMinute(key, limits.PerUserPerMinute);
            });
            options.AddPolicy(IcalPolicy, context => PerMinute($"ip:{ClientIp(context)}", Limits(context).IcalPerIpPerMinute));
        });

        return services;
    }

    private static GatewayRateLimitOptions Limits(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<GatewayRateLimitOptions>>().Value;

    private static string ClientIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static RateLimitPartition<string> PerMinute(string key, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });

    private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await ProblemResponses.WriteAsync(context.HttpContext, ErrorCodes.RateLimited, "Too many requests.");
    }
}
