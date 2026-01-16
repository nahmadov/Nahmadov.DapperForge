namespace Nahmadov.DapperForge.Core.Modeling.Mapping;
/// <summary>
/// Holds mutable configuration collected via the fluent API for an entity type.
/// </summary>
public class EntityConfig(Type clrType)
{
    public Type ClrType { get; } = clrType;
    public string? TableName { get; private set; }
    public string? Schema { get; private set; }
    public bool IsReadOnly { get; private set; }
    public bool HasKey { get; private set; } = true;
    public List<string> KeyProperties { get; } = new();
    public List<string> AlternateKeyProperties { get; } = new();
    public Dictionary<string, PropertyConfig> Properties { get; } = [];
    public List<RelationshipConfig> Relationships { get; } = new();

    /// <summary>
    /// Configures the table name and optional schema for the entity.
    /// </summary>
    /// <param name="tableName">Target table name.</param>
    /// <param name="schema">Optional schema name.</param>
    public void SetTable(string tableName, string? schema = null)
    {
        TableName = tableName;
        Schema = schema;
    }

    /// <summary>
    /// Sets only the table name while preserving the existing schema value.
    /// </summary>
    public void SetTableName(string tableName) => SetTable(tableName, Schema);

    /// <summary>
    /// Marks the entity as read-only, disabling write operations.
    /// </summary>
    public void SetReadOnly(bool isReadOnly) => IsReadOnly = isReadOnly;

    /// <summary>
    /// Marks the entity as having no key defined.
    /// </summary>
    public void SetNoKey() => HasKey = false;
}

