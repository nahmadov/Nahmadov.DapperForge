using System.Reflection;

namespace Nahmadov.DapperForge.Core.Mapping;

/// <summary>
/// Represents a foreign key relationship between two entities.
/// </summary>
public sealed class ForeignKeyMapping
{
    /// <summary>
    /// Gets the navigation property on the principal entity.
    /// </summary>
    public PropertyInfo NavigationProperty { get; }

    /// <summary>
    /// Gets the foreign key property (the column that holds the reference).
    /// </summary>
    public PropertyInfo ForeignKeyProperty { get; }

    /// <summary>
    /// Gets the type of the related entity.
    /// </summary>
    public Type PrincipalEntityType { get; }

    /// <summary>
    /// Gets the name of the foreign key column in the database.
    /// </summary>
    public string ForeignKeyColumnName { get; }

    /// <summary>
    /// Gets the name of the primary key column in the related table.
    /// </summary>
    public string PrincipalKeyColumnName { get; }

    /// <summary>
    /// Gets the name of the related table.
    /// </summary>
    public string PrincipalTableName { get; }

    /// <summary>
    /// Gets the schema of the related table (if any).
    /// </summary>
    public string? PrincipalSchema { get; }

    /// <summary>
    /// Initializes a new foreign key mapping.
    /// </summary>
    /// <param name="navigationProperty">Navigation property on the dependent entity.</param>
    /// <param name="foreignKeyProperty">Foreign key property (scalar value).</param>
    /// <param name="principalEntityType">Type of the related entity.</param>
    /// <param name="foreignKeyColumnName">Database column name for the foreign key.</param>
    /// <param name="principalKeyColumnName">Database column name for the principal key.</param>
    /// <param name="principalTableName">Table name of the related entity.</param>
    /// <param name="principalSchema">Schema of the related table (optional).</param>
    public ForeignKeyMapping(
        PropertyInfo navigationProperty,
        PropertyInfo foreignKeyProperty,
        Type principalEntityType,
        string foreignKeyColumnName,
        string principalKeyColumnName,
        string principalTableName,
        string? principalSchema = null)
    {
        NavigationProperty = navigationProperty ?? throw new ArgumentNullException(nameof(navigationProperty));
        ForeignKeyProperty = foreignKeyProperty ?? throw new ArgumentNullException(nameof(foreignKeyProperty));
        PrincipalEntityType = principalEntityType ?? throw new ArgumentNullException(nameof(principalEntityType));
        ForeignKeyColumnName = foreignKeyColumnName ?? throw new ArgumentNullException(nameof(foreignKeyColumnName));
        PrincipalKeyColumnName = principalKeyColumnName ?? throw new ArgumentNullException(nameof(principalKeyColumnName));
        PrincipalTableName = principalTableName ?? throw new ArgumentNullException(nameof(principalTableName));
        PrincipalSchema = principalSchema;
    }
}
