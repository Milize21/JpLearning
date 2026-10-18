using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Nomax.SharedKernel.Serialization;

namespace Nomax.BuildingBlocks.Persistence;

/// <summary>
/// Every enum property becomes a text column holding snake_case values, guarded by a CHECK constraint
/// named <c>ck_{table}_{column}</c> (docs/03 §1 rule 6). An explicit HasConversion in a configuration overrides this.
/// </summary>
internal sealed class SnakeCaseEnumConvention : IPropertyAddedConvention, IModelFinalizingConvention
{
    public void ProcessPropertyAdded(IConventionPropertyBuilder propertyBuilder, IConventionContext<IConventionPropertyBuilder> context)
    {
        if (EnumTypeOf(propertyBuilder.Metadata.ClrType) is { } enumType)
        {
            propertyBuilder.HasConversion(SnakeCaseEnumConverter.Create(enumType));
        }
    }

    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (entityType.GetTableName() is not { } table)
            {
                continue;
            }

            var storeObject = StoreObjectIdentifier.Table(table, entityType.GetSchema());
            foreach (var property in entityType.GetDeclaredProperties())
            {
                if (EnumTypeOf(property.ClrType) is not { } enumType
                    || !SnakeCaseEnumConverter.IsSnakeCaseEnumConverter(property.GetValueConverter())
                    || property.GetColumnName(storeObject) is not { } column)
                {
                    continue;
                }

                var values = string.Join(", ", EnumSnakeCase.Values(enumType).Select(value => $"'{value}'"));
                entityType.Builder.HasCheckConstraint(
                    string.Create(CultureInfo.InvariantCulture, $"ck_{table}_{column}"),
                    string.Create(CultureInfo.InvariantCulture, $"\"{column}\" IN ({values})"));
            }
        }
    }

    private static Type? EnumTypeOf(Type clrType) =>
        (Nullable.GetUnderlyingType(clrType) ?? clrType) is { IsEnum: true } enumType ? enumType : null;
}

/// <summary>
/// <c>created_at</c>/<c>updated_at</c> default to <c>now()</c> in the database (docs/03 §1 rule 4).
/// </summary>
internal sealed class TimestampsConvention : IModelFinalizingConvention
{
    private static readonly string[] TimestampProperties = [nameof(IHasTimestamps.CreatedAt), nameof(IHasTimestamps.UpdatedAt)];

    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (!typeof(IHasTimestamps).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            foreach (var name in TimestampProperties)
            {
                entityType.FindProperty(name)?.Builder.HasDefaultValueSql("now()");
            }
        }
    }
}
