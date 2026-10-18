namespace Nomax.SharedKernel.Results;

/// <summary>
/// An expected failure of a use case. <see cref="Code"/> comes from <see cref="ErrorCodes"/>;
/// <see cref="Message"/> is English text for developers and is never shown to users as-is.
/// </summary>
public sealed record AppError(string Code, string Message, IReadOnlyDictionary<string, string[]>? ValidationErrors = null)
{
    public static AppError Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(ErrorCodes.ValidationFailed, "One or more validation errors occurred.", errors);

    /// <summary>Also used for resources owned by someone else, to prevent enumeration (docs/01 §6).</summary>
    public static AppError NotFound(string message = "The resource was not found.") => new(ErrorCodes.NotFound, message);

    public static AppError Forbidden(string message = "You are not allowed to perform this action.") =>
        new(ErrorCodes.Forbidden, message);

    public static AppError Conflict(string message) => new(ErrorCodes.Conflict, message);

    public static AppError InvalidState(string message) => new(ErrorCodes.InvalidState, message);
}
