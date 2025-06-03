using DapperToolkit.Core.Attributes;

namespace DapperToolkit.SqlServerTests.IntegrationTests;

[TableName("TestEntities")]
public class TestEntity
{
    [ColumnName("Identifier")]
    public int Id { get; set; }

    [ColumnName("NameNew")]
    public string Name { get; set; } = string.Empty;
}