using System.Diagnostics.CodeAnalysis;

namespace Nomax.SharedKernel.Results;

/// <summary>
/// Outcome of a use case. Expected failures travel as <see cref="AppError"/> instead of exceptions.
/// </summary>
public class Result
{
    private static readonly Result SuccessInstance = new(null);

    private protected Result(AppError? error) => Error = error;

    public AppError? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => SuccessInstance;

    public static Result<T> Success<T>(T value) => new(value);

    public static Result Failure(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    public static Result<T> Failure<T>(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }

    public static implicit operator Result(AppError error) => Failure(error);
}

/// <summary>
/// Outcome of a use case that produces a <typeparamref name="T"/> on success.
/// Create it with <see cref="Result.Success{T}(T)"/>, <see cref="Result.Failure{T}(AppError)"/> or an implicit conversion.
/// </summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T value)
        : base(null) => _value = value;

    internal Result(AppError error)
        : base(error)
    {
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failed result cannot be read.");

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(AppError error) => Failure<T>(error);
}
