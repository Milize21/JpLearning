using System.Text.Json.Serialization;

namespace Nomax.SharedKernel.Text;

/// <summary>
/// Short learning-content text with a Japanese companion (docs/03 §3.1). Which parts are required depends on where it is used;
/// that is checked by the validators of each use case, not here.
/// </summary>
public sealed record TextBlock(
    [property: JsonPropertyName("id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Indonesian,
    [property: JsonPropertyName("en"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? English,
    [property: JsonPropertyName("ja"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<JaSegment>? Japanese);
