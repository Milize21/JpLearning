using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Content.Domain;
using Nomax.Modules.Content.Infrastructure;
using Nomax.Modules.Content.Infrastructure.Files;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Content.Features.Files;

/// <summary>
/// GET /api/content/files/{id}/content?exp=…&amp;sig=… — the signed link a resource endpoint redirects to (BR-FILE-04).
/// No token needed; a wrong or expired signature is 404. Files are never rendered inline (BR-FILE-02): attachment,
/// octet-stream and nosniff, except verified cover images when the link was signed for inline display.
/// </summary>
internal sealed class DownloadFileHandler(ContentDbContext db, SignedUrls signedUrls, IFileStorage storage)
{
    public async Task<IResult> HandleAsync(HttpContext httpContext, Guid id, long exp, string? sig, int? inline, CancellationToken cancellationToken)
    {
        var showInline = inline == 1;
        if (!signedUrls.Verify(id, exp, sig, showInline))
        {
            return AppError.NotFound().ToProblem();
        }

        var file = await db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(file => file.Id == id && file.Status == FileStatus.Ready, cancellationToken);
        var stream = file is null ? null : await storage.OpenReadAsync(file.StorageKey, cancellationToken);
        if (file is null || stream is null)
        {
            return AppError.NotFound().ToProblem();
        }

        var headers = httpContext.Response.Headers;
        headers[HeaderNames.XContentTypeOptions] = "nosniff";
        headers[HeaderNames.CacheControl] = "private, no-store";

        if (showInline && file.Purpose == FilePurpose.BookCover && file.SizeBytes <= FileRules.MaxInlineCoverBytes)
        {
            var header = new byte[12];
            var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
            stream.Position = 0;
            if (FileRules.DetectImageType(header.AsSpan(0, read)) is { } imageType)
            {
                return Results.File(stream, imageType);
            }
        }

        return Results.File(stream, "application/octet-stream", file.OriginalName, enableRangeProcessing: true);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/files/{id:guid}/content", (HttpContext httpContext, Guid id, long exp, string? sig, int? inline, DownloadFileHandler handler, CancellationToken cancellationToken) =>
                handler.HandleAsync(httpContext, id, exp, sig, inline, cancellationToken))
            .AllowAnonymous()
            .WithName("DownloadFile")
            .WithSummary("Download through a signed link (no token; links expire after a few minutes)")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .ProducesProblem(StatusCodes.Status404NotFound);
}
