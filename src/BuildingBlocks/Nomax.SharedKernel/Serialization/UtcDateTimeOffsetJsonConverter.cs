using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nomax.SharedKernel.Serialization;

/// <summary>
/// Writes every <see cref="DateTimeOffset"/> as ISO 8601 UTC ending in <c>Z</c> (docs/05 §1);
/// the default converter would write <c>+00:00</c>. Incoming offsets are normalized to UTC.
/// </summary>
public sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset().ToUniversalTime();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.UtcDateTime);
    }
}
