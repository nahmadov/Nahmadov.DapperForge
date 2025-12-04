namespace DapperToolkit.Core.Mapping;

public class EntityConfig(Type clrType)
{
    public Type ClrType { get; } = clrType;
    public string TableName { get; private set; } = default!;
    public string? Schema { get; private set; }
    public bool IsReadOnly { get; private set; }
    public bool HasKey { get; private set; } = true;
    public List<string> KeyProperties { get; } = new();
    public Dictionary<string, PropertyConfig> Properties { get; } = [];

    public void SetTable(string tableName, string? schema = null)
    {
        TableName = tableName;
        Schema = schema;
    }

    public void SetTableName(string tableName) => SetTable(tableName, Schema);
    public void SetReadOnly(bool isReadOnly) => IsReadOnly = isReadOnly;
    public void SetNoKey() => HasKey = false;
}
