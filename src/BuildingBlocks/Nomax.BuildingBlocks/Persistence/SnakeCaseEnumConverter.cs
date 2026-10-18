using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nomax.SharedKernel.Serialization;

namespace Nomax.BuildingBlocks.Persistence;

/// <summary>
/// Stores an enum as its snake_case name in a text column (docs/03 §1 rule 6, D-08).
/// </summary>
public sealed class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => EnumSnakeCase.ToValue(value),
    text => EnumSnakeCase.Parse<TEnum>(text))
    where TEnum : struct, Enum;

internal static class SnakeCaseEnumConverter
{
    public static ValueConverter Create(Type enumType) =>
        (ValueConverter)Activator.CreateInstance(typeof(SnakeCaseEnumConverter<>).MakeGenericType(enumType))!;

    public static bool IsSnakeCaseEnumConverter(ValueConverter? converter) =>
        converter?.GetType() is { IsGenericType: true } type
        && type.GetGenericTypeDefinition() == typeof(SnakeCaseEnumConverter<>);
}
