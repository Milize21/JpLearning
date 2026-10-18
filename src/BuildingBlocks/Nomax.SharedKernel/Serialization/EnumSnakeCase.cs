using System.Collections.Frozen;
using System.Text.Json;

namespace Nomax.SharedKernel.Serialization;

/// <summary>
/// Converts enums to and from the lower snake_case strings stored in text + CHECK columns and sent over JSON
/// (docs/03 §1 rule 6, D-08). JSON and EF Core use this same naming so the two never drift apart.
/// </summary>
public static class EnumSnakeCase
{
    public static string ToSnakeCase(string name) => JsonNamingPolicy.SnakeCaseLower.ConvertName(name);

    public static string ToValue<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        Cache<TEnum>.ToValue.TryGetValue(value, out var text)
            ? text
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"Undefined {typeof(TEnum).Name} value.");

    public static TEnum Parse<TEnum>(string value)
        where TEnum : struct, Enum =>
        TryParse<TEnum>(value, out var result)
            ? result
            : throw new FormatException($"'{value}' is not a valid {typeof(TEnum).Name} value.");

    public static bool TryParse<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        if (value is not null && Cache<TEnum>.FromValue.TryGetValue(value, out result))
        {
            return true;
        }

        result = default;
        return false;
    }

    public static IReadOnlyList<string> Values<TEnum>()
        where TEnum : struct, Enum => Cache<TEnum>.Values;

    public static IReadOnlyList<string> Values(Type enumType)
    {
        ArgumentNullException.ThrowIfNull(enumType);
        if (!enumType.IsEnum)
        {
            throw new ArgumentException($"{enumType.Name} is not an enum type.", nameof(enumType));
        }

        return [.. Enum.GetNames(enumType).Select(ToSnakeCase)];
    }

    private static class Cache<TEnum>
        where TEnum : struct, Enum
    {
        public static readonly FrozenDictionary<TEnum, string> ToValue =
            Enum.GetValues<TEnum>().ToFrozenDictionary(e => e, e => ToSnakeCase(e.ToString()));

        public static readonly FrozenDictionary<string, TEnum> FromValue =
            ToValue.ToFrozenDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

        public static readonly IReadOnlyList<string> Values = [.. ToValue.Values.Order(StringComparer.Ordinal)];
    }
}
