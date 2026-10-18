using System.Text.Json.Serialization;

namespace Nomax.SharedKernel.Text;

/// <summary>
/// Long reading material in restricted Markdown with furigana markup (docs/03 §3.2).
/// </summary>
public sealed record RichText(
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("id")] string Indonesian,
    [property: JsonPropertyName("en"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? English)
{
    public const string MarkdownFormat = "md";
}
