namespace Nomax.Gateway;

/// <summary>
/// Security headers from docs/09 §4 on every response, proxied ones included.
/// The SPA policy (script/style/connect sources) is added when the Gateway starts serving the web build in production.
/// </summary>
public static class SecurityHeaders
{
    // Everything served here today is JSON, ICS or a redirect, so nothing may be loaded or framed.
    public const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

    private static readonly PathString ScalarPath = new("/scalar");

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

                // The development-only Scalar UI needs its own scripts and styles.
                if (!context.Request.Path.StartsWithSegments(ScalarPath))
                {
                    headers.ContentSecurityPolicy = ApiContentSecurityPolicy;
                }

                return Task.CompletedTask;
            });
            await next(context);
        });
}
