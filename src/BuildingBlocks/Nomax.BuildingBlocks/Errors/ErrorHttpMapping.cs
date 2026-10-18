using Microsoft.AspNetCore.Http;
using Nomax.SharedKernel.Results;

namespace Nomax.BuildingBlocks.Errors;

/// <summary>
/// HTTP side of the error catalog in docs/05 §2.
/// </summary>
public static class ErrorHttpMapping
{
    public const string CodeExtension = "code";

    public static int StatusCodeFor(string code) => code switch
    {
        ErrorCodes.ValidationFailed => StatusCodes.Status400BadRequest,
        ErrorCodes.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorCodes.Forbidden or ErrorCodes.AccountInactive or ErrorCodes.ChapterLocked => StatusCodes.Status403Forbidden,
        ErrorCodes.NotFound => StatusCodes.Status404NotFound,
        ErrorCodes.MethodNotAllowed => StatusCodes.Status405MethodNotAllowed,
        ErrorCodes.Conflict
            or ErrorCodes.ConcurrencyConflict
            or ErrorCodes.HasDependents
            or ErrorCodes.InvalidState
            or ErrorCodes.AlreadySubmitted
            or ErrorCodes.AttemptsExhausted
            or ErrorCodes.AttemptExpired
            or ErrorCodes.QuizNotOpen
            or ErrorCodes.SessionEnded
            or ErrorCodes.EmptyPool
            or ErrorCodes.FileNotReady => StatusCodes.Status409Conflict,
        ErrorCodes.PayloadTooLarge or ErrorCodes.FileTooLarge => StatusCodes.Status413PayloadTooLarge,
        ErrorCodes.UnsupportedMediaType => StatusCodes.Status415UnsupportedMediaType,
        ErrorCodes.RateLimited => StatusCodes.Status429TooManyRequests,
        ErrorCodes.KeycloakUnavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>Code for a response produced by the framework itself (routing, authentication, Kestrel limits).</summary>
    public static string CodeForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => ErrorCodes.ValidationFailed,
        StatusCodes.Status401Unauthorized => ErrorCodes.Unauthorized,
        StatusCodes.Status403Forbidden => ErrorCodes.Forbidden,
        StatusCodes.Status404NotFound => ErrorCodes.NotFound,
        StatusCodes.Status405MethodNotAllowed => ErrorCodes.MethodNotAllowed,
        StatusCodes.Status409Conflict => ErrorCodes.Conflict,
        StatusCodes.Status413PayloadTooLarge => ErrorCodes.PayloadTooLarge,
        StatusCodes.Status415UnsupportedMediaType => ErrorCodes.UnsupportedMediaType,
        StatusCodes.Status429TooManyRequests => ErrorCodes.RateLimited,
        _ => ErrorCodes.InternalError,
    };

    public static string TypeFor(string code) => $"https://nomax.local/errors/{code}";

    /// <summary>English title for developers, e.g. <c>account_inactive</c> → "Account inactive".</summary>
    public static string TitleFor(string code)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        var words = code.Replace('_', ' ');
        return string.Concat(char.ToUpperInvariant(words[0]).ToString(), words[1..]);
    }
}
