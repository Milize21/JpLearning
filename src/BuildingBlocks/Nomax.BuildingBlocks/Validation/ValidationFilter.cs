using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Nomax.BuildingBlocks.Errors;
using Nomax.SharedKernel.Results;

namespace Nomax.BuildingBlocks.Validation;

/// <summary>
/// Runs the FluentValidation validator of <typeparamref name="TRequest"/> before the handler and answers
/// 400 <c>validation_failed</c> with field errors keyed in camelCase, matching the JSON body (docs/05 §2).
/// </summary>
public sealed class ValidationFilter<TRequest> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var validator = context.HttpContext.RequestServices.GetService<IValidator<TRequest>>()
            ?? throw new InvalidOperationException($"No IValidator<{typeof(TRequest).Name}> is registered.");

        if (context.Arguments.OfType<TRequest>().FirstOrDefault() is not { } request)
        {
            return await next(context);
        }

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        return result.IsValid
            ? await next(context)
            : AppError.Validation(ValidationErrors.From(result)).ToProblem();
    }
}

public static class ValidationErrors
{
    public static Dictionary<string, string[]> From(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Errors
            .GroupBy(failure => ToCamelCasePath(failure.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
    }

    /// <summary><c>Items[0].DisplayName</c> → <c>items[0].displayName</c>.</summary>
    public static string ToCamelCasePath(string propertyPath) =>
        string.Join('.', (propertyPath ?? string.Empty).Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}

public static class ValidationEndpointExtensions
{
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<ValidationFilter<TRequest>>().ProducesValidationProblem();
}
