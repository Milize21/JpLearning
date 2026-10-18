using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Nomax.SharedKernel.Serialization;

/// <summary>
/// The one JSON convention used for the API, jsonb columns and integration events (docs/05 §1):
/// camelCase properties, snake_case enum values, UTC timestamps ending in <c>Z</c>.
/// </summary>
public static class NomaxJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        // Numbers are numbers: the web defaults would also accept "5" and publish number|string in OpenAPI.
        options.NumberHandling = JsonNumberHandling.Strict;
        // Japanese text stays readable instead of \uXXXX; HTML-sensitive characters are still escaped.
        options.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
        options.Converters.Add(new UtcDateTimeOffsetJsonConverter());
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
