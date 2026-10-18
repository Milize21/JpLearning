using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Nomax.BuildingBlocks.Errors;

/// <summary>
/// RFC 9457 Problem Details with the stable <c>code</c> extension (docs/05 §2).
/// </summary>
public static class ProblemResponses
{
    public static IServiceCollection AddNomaxProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = Customize);
        services.AddExceptionHandler<NomaxExceptionHandler>();
        return services;
    }

    public static ProblemDetails Create(string code, string? detail = null) => new()
    {
        Status = ErrorHttpMapping.StatusCodeFor(code),
        Type = ErrorHttpMapping.TypeFor(code),
        Title = ErrorHttpMapping.TitleFor(code),
        Detail = detail,
        Extensions = { [ErrorHttpMapping.CodeExtension] = code },
    };

    /// <summary>Writes a problem response from middleware, where no endpoint result is available.</summary>
    public static async Task WriteAsync(HttpContext httpContext, string code, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var problem = Create(code, detail);
        httpContext.Response.StatusCode = problem.Status!.Value;
        var problemDetails = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.WriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });
    }

    // Runs for every problem response, including the ones the framework produces for bare status codes.
    internal static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;
        problem.Status = status;

        if (!problem.Extensions.TryGetValue(ErrorHttpMapping.CodeExtension, out var value) || value is not string code)
        {
            code = ErrorHttpMapping.CodeForStatus(status);
            problem.Extensions[ErrorHttpMapping.CodeExtension] = code;
        }

        problem.Type = ErrorHttpMapping.TypeFor(code);
        problem.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
    }
}
