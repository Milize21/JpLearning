using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;

namespace Nomax.BuildingBlocks.Hosting;

/// <summary>
/// The 1 MB body limit (docs/05 §1) does not apply to tus uploads: clients send 8 MB chunks (BR-FILE-03). The API raises
/// the limit per endpoint through metadata; the Gateway, which has no endpoint for it, by path.
/// </summary>
public static class UploadLimits
{
    public const string UploadPathPrefix = "/api/content/uploads";

    /// <summary>One chunk of 8 MB plus headroom.</summary>
    public const long MaxChunkBodyBytes = 16 * 1024 * 1024;

    public static TBuilder WithUploadBodyLimit<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new UploadBodySize());
        return builder;
    }

    /// <summary>For the Gateway: raises the limit for requests to <see cref="UploadPathPrefix"/> before the body is read.</summary>
    public static IApplicationBuilder UseUploadBodyLimit(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(UploadPathPrefix, StringComparison.OrdinalIgnoreCase)
                && context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature)
            {
                feature.MaxRequestBodySize = MaxChunkBodyBytes;
            }

            return next(context);
        });

    private sealed class UploadBodySize : IRequestSizeLimitMetadata
    {
        public long? MaxRequestBodySize => MaxChunkBodyBytes;
    }
}
