using Microsoft.AspNetCore.Http;
using Nomax.SharedKernel.Results;

namespace Nomax.BuildingBlocks.Errors;

/// <summary>
/// Maps use-case results to HTTP responses so endpoints stay one-liners.
/// </summary>
public static class ResultHttpExtensions
{
    public static IResult ToProblem(this AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var extensions = new Dictionary<string, object?> { [ErrorHttpMapping.CodeExtension] = error.Code };

        if (error.ValidationErrors is { } errors)
        {
            return TypedResults.ValidationProblem(
                errors,
                detail: error.Message,
                type: ErrorHttpMapping.TypeFor(error.Code),
                title: ErrorHttpMapping.TitleFor(error.Code),
                extensions: extensions);
        }

        return TypedResults.Problem(
            detail: error.Message,
            statusCode: ErrorHttpMapping.StatusCodeFor(error.Code),
            title: ErrorHttpMapping.TitleFor(error.Code),
            type: ErrorHttpMapping.TypeFor(error.Code),
            extensions: extensions);
    }

    public static IResult ToHttpResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
    }

    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem();
    }

    public static IResult ToOkResult<T>(this Result<T> result) => result.ToHttpResult(value => TypedResults.Ok(value));
}
