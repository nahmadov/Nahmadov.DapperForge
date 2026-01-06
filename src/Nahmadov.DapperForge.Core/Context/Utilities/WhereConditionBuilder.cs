using System.Reflection;

using Nahmadov.DapperForge.Core.Exceptions;
using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Context.Utilities;

/// <summary>
/// Builds safe, parameterized WHERE conditions for Update/Delete operations.
/// Validates column names against entity mapping to prevent SQL injection.
/// </summary>
internal static class WhereConditionBuilder
{
    /// <summary>
    /// Builds a WHERE clause using the provided key properties and entity instance.
    /// </summary>
    /// <param name="keyProperties">Key properties to use for the WHERE clause.</param>
    /// <param name="mapping">Entity mapping for column name resolution.</param>
    /// <param name="dialect">SQL dialect for identifier quoting.</param>
    /// <param name="entity">Entity instance to extract key values from.</param>
    /// <param name="entityName">Entity type name for error messages.</param>
    /// <returns>Tuple of WHERE clause SQL and parameter dictionary.</returns>
    public static (string whereClause, Dictionary<string, object?> parameters) BuildFromKey<TEntity>(
        IReadOnlyList<PropertyInfo> keyProperties,
        EntityMapping mapping,
        ISqlDialect dialect,
        TEntity entity,
        string entityName) where TEntity : class
    {
        if (keyProperties.Count == 0)
            throw new InvalidOperationException(
                $"Cannot build WHERE clause for entity '{entityName}': no key properties available.");

        var whereClauses = new List<string>();
        var parameters = new Dictionary<string, object?>();

        foreach (var keyProp in keyProperties)
        {
            var propMapping = mapping.PropertyMappings.FirstOrDefault(pm => pm.Property == keyProp)
                ?? throw new DapperConfigurationException(
                    entityName,
                    $"Key property '{keyProp.Name}' not found in property mappings.");

            var columnName = dialect.QuoteIdentifier(propMapping.ColumnName);
            var paramName = $"@{keyProp.Name}";
            var value = keyProp.GetValue(entity);

            whereClauses.Add($"{columnName} = {paramName}");
            parameters[keyProp.Name] = value;
        }

        return (string.Join(" AND ", whereClauses), parameters);
    }

    /// <summary>
    /// Validates and builds a WHERE clause from explicit column/value pairs.
    /// Ensures all columns exist in entity mapping to prevent SQL injection.
    /// </summary>
    /// <param name="whereConditions">Dictionary of column names to values.</param>
    /// <param name="mapping">Entity mapping for validation.</param>
    /// <param name="dialect">SQL dialect for identifier quoting.</param>
    /// <param name="entityName">Entity type name for error messages.</param>
    /// <returns>Tuple of WHERE clause SQL and parameter dictionary.</returns>
    public static (string whereClause, Dictionary<string, object?> parameters) BuildFromExplicitConditions(
        IDictionary<string, object?> whereConditions,
        EntityMapping mapping,
        ISqlDialect dialect,
        string entityName)
    {
        if (whereConditions == null || whereConditions.Count == 0)
            throw new InvalidOperationException(
                $"WHERE conditions cannot be empty for entity '{entityName}'.");

        var whereClauses = new List<string>();
        var parameters = new Dictionary<string, object?>();

        foreach (var (columnName, value) in whereConditions)
        {
            // Validate that column exists in entity mapping
            var propMapping = mapping.PropertyMappings.FirstOrDefault(pm =>
                string.Equals(pm.Property.Name, columnName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pm.ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                ?? throw new DapperConfigurationException(
                    entityName,
                    $"Column '{columnName}' not found in entity mapping. Only mapped properties are allowed in WHERE conditions.");

            var quotedColumn = dialect.QuoteIdentifier(propMapping.ColumnName);
            var paramName = $"@{propMapping.Property.Name}";

            if (value == null)
            {
                whereClauses.Add($"{quotedColumn} IS NULL");
            }
            else
            {
                whereClauses.Add($"{quotedColumn} = {paramName}");
                parameters[propMapping.Property.Name] = value;
            }
        }

        return (string.Join(" AND ", whereClauses), parameters);
    }

    /// <summary>
    /// Validates that unsafe WHERE patterns are not present.
    /// </summary>
    public static void ValidateSafety(string? whereClause, string entityName)
    {
        if (string.IsNullOrWhiteSpace(whereClause))
            throw new InvalidOperationException(
                $"WHERE clause cannot be empty for entity '{entityName}'.");

        // Check for dangerous patterns
        var normalized = whereClause.Trim().ToUpperInvariant();

        if (normalized == "1 = 1" || normalized == "1=1")
            throw new DapperOperationException(
                OperationType.Update,
                entityName,
                "WHERE clause '1 = 1' is forbidden as it would affect all rows.");

        if (normalized == "TRUE" || normalized == "1")
            throw new DapperOperationException(
                OperationType.Update,
                entityName,
                "WHERE clause that always evaluates to TRUE is forbidden.");
    }
}
