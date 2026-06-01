using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;

namespace Nahmadov.DapperForge.Core.Schema;

/// <summary>
/// Projects an entity's resolved <see cref="EntityMapping"/> onto temp-table columns, so a staging
/// table mirrors the column names and types DapperForge already uses for the entity.
/// </summary>
internal static class EntityTempTableSchema
{
    /// <summary>
    /// Adds a temp-table column for each mapped property.
    /// </summary>
    /// <param name="builder">The temp-table builder to populate.</param>
    /// <param name="mapping">The entity mapping providing column names and types.</param>
    /// <param name="includedProperties">
    /// When non-null, only properties whose names appear here are emitted (explicit subset, in the
    /// given order). When null, all mapped writable columns are emitted and database-generated /
    /// identity columns are excluded.
    /// </param>
    public static ITempTableBuilder Populate(
        ITempTableBuilder builder,
        EntityMapping mapping,
        IReadOnlyList<string>? includedProperties = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(mapping);

        var selected = SelectProperties(mapping, includedProperties);

        foreach (var pm in selected)
        {
            var (type, facets) = pm.ResolveColumnType();
            builder.Column(pm.ColumnName, type, facets with { IsNullable = IsNullable(pm) });
        }

        return builder;
    }

    private static IEnumerable<PropertyMapping> SelectProperties(
        EntityMapping mapping,
        IReadOnlyList<string>? includedProperties)
    {
        if (includedProperties is null)
        {
            // Mirror writable columns; exclude identity / computed / read-only / sequence columns.
            return mapping.PropertyMappings.Where(pm => !pm.IsGenerated);
        }

        // Explicit subset: emit exactly the chosen properties, in caller order. An explicit choice
        // overrides the default exclusion of generated columns.
        var byName = mapping.PropertyMappings.ToDictionary(pm => pm.Property.Name, StringComparer.Ordinal);

        return includedProperties.Select(name =>
            byName.TryGetValue(name, out var pm)
                ? pm
                : throw new InvalidOperationException(
                    $"Property '{name}' is not mapped on entity '{mapping.EntityType.Name}'."));
    }

    /// <summary>
    /// A column is nullable when the CLR type is nullable (reference type or <see cref="Nullable{T}"/>)
    /// and the property is not required.
    /// </summary>
    private static bool IsNullable(PropertyMapping pm)
    {
        var type = pm.Property.PropertyType;
        var clrNullable = Nullable.GetUnderlyingType(type) is not null || !type.IsValueType;
        return clrNullable && !pm.IsRequired;
    }
}
