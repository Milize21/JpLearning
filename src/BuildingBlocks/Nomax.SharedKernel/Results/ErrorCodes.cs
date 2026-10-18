namespace Nomax.SharedKernel.Results;

/// <summary>
/// Stable, machine-readable error codes from the catalog in docs/05 §2.
/// The frontend maps every code to an <c>errors.&lt;code&gt;</c> label, so a new code must be added there too.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string AccountInactive = "account_inactive";
    public const string ChapterLocked = "chapter_locked";
    public const string NotFound = "not_found";
    public const string MethodNotAllowed = "method_not_allowed";
    public const string Conflict = "conflict";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string HasDependents = "has_dependents";
    public const string InvalidState = "invalid_state";
    public const string AlreadySubmitted = "already_submitted";
    public const string AttemptsExhausted = "attempts_exhausted";
    public const string AttemptExpired = "attempt_expired";
    public const string QuizNotOpen = "quiz_not_open";
    public const string SessionEnded = "session_ended";
    public const string EmptyPool = "empty_pool";
    public const string FileNotReady = "file_not_ready";
    public const string PayloadTooLarge = "payload_too_large";
    public const string FileTooLarge = "file_too_large";
    public const string UnsupportedMediaType = "unsupported_media_type";
    public const string RateLimited = "rate_limited";
    public const string KeycloakUnavailable = "keycloak_unavailable";
    public const string InternalError = "internal_error";
}
