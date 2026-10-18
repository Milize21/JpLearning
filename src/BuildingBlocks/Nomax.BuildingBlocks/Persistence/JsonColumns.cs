using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nomax.SharedKernel.Serialization;

namespace Nomax.BuildingBlocks.Persistence;

/// <summary>
/// The single jsonb mapping used by every module (docs/06 §10): System.Text.Json with the API conventions (camelCase keys,
/// so TextBlock is stored as <c>{"id","en","ja":[{"t","r"}]}</c>), and a comparer on the serialized form.
/// </summary>
public static class JsonColumns
{
    public static PropertyBuilder<T> HasJsonbConversion<T>(this PropertyBuilder<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        property
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, NomaxJson.Options),
                json => JsonSerializer.Deserialize<T>(json, NomaxJson.Options)!,
                new ValueComparer<T>(
                    (left, right) => Serialize(left) == Serialize(right),
                    value => Serialize(value).GetHashCode(StringComparison.Ordinal),
                    // Stored values are immutable records, so the snapshot can share the instance.
                    value => value));
        return property;
    }

    private static string Serialize<T>(T value) => value is null ? string.Empty : JsonSerializer.Serialize(value, NomaxJson.Options);
}
