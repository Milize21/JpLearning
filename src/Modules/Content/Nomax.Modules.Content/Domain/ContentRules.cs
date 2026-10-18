using System.Text.RegularExpressions;

namespace Nomax.Modules.Content.Domain;

/// <summary>Field rules of the content structure (docs/03 §5).</summary>
public static partial class ContentRules
{
    public const int MaxChapterNumber = 9999;

    /// <summary>Level codes such as <c>dasar</c>, <c>n5</c>: 1–16 characters of a-z, 0-9 or hyphen.</summary>
    public static bool IsValidLevelCode(string? code) => code is not null && LevelCode().IsMatch(code);

    public static bool IsValidSortOrder(int sortOrder) => sortOrder >= 0;

    [GeneratedRegex("^[a-z0-9-]{1,16}$")]
    private static partial Regex LevelCode();
}
