namespace Nomax.BuildingBlocks.Persistence;

/// <summary>
/// Escapes user text for LIKE/ILIKE patterns, where % and _ are wildcards. Always pass <see cref="EscapeCharacter"/>
/// to <c>EF.Functions.ILike</c>: without it Npgsql emits <c>ESCAPE ''</c> and the backslashes lose their meaning.
/// </summary>
public static class SqlLike
{
    public const string EscapeCharacter = @"\";

    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text
            .Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter, StringComparison.Ordinal)
            .Replace("%", EscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", EscapeCharacter + "_", StringComparison.Ordinal);
    }

    /// <summary>Case-insensitive "contains" pattern for ILIKE.</summary>
    public static string Contains(string text) => $"%{Escape(text)}%";
}
