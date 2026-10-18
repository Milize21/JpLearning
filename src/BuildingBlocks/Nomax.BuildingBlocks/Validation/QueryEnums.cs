using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Serialization;

namespace Nomax.BuildingBlocks.Validation;

/// <summary>
/// Enum values in query strings use the same snake_case names as JSON (docs/05 §1), e.g. <c>?role=murid</c>.
/// Minimal API binding of enums would expect the C# member name instead, so list endpoints take a string and parse it here.
/// </summary>
public static class QueryEnums
{
    /// <returns>The parsed value, null when absent, or a validation error naming <paramref name="parameter"/>.</returns>
    public static Result<TEnum?> Parse<TEnum>(string? value, string parameter)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrEmpty(value))
        {
            return Result.Success<TEnum?>(null);
        }

        return EnumSnakeCase.TryParse<TEnum>(value, out var parsed)
            ? Result.Success<TEnum?>(parsed)
            : Result.Failure<TEnum?>(AppError.Validation(new Dictionary<string, string[]>
            {
                [parameter] = [$"Must be one of: {string.Join(", ", EnumSnakeCase.Values<TEnum>())}."],
            }));
    }
}
