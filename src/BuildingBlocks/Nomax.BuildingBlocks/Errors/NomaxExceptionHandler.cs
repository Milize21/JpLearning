using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Nomax.BuildingBlocks.Errors;

/// <summary>
/// Turns exceptions into Problem Details. Stack traces and messages stay in the server log; the client only gets the code.
/// </summary>
internal sealed partial class NomaxExceptionHandler(IProblemDetailsService problemDetails, ILogger<NomaxExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Kestrel and request binding report malformed or oversized requests as BadHttpRequestException.
        var status = exception is BadHttpRequestException badRequest
            ? badRequest.StatusCode
            : StatusCodes.Status500InternalServerError;

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception);
        }
        else
        {
            LogBadRequest(logger, status, exception);
        }

        var problem = ProblemResponses.Create(ErrorHttpMapping.CodeForStatus(status));
        problem.Status = status;
        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rejected bad request with status {StatusCode}")]
    private static partial void LogBadRequest(ILogger logger, int statusCode, Exception exception);
}
