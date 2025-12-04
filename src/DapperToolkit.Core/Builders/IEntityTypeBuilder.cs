namespace DapperToolkit.Core.Builders;

public interface IEntityTypeBuilder
{
    string? TableName { get; }
    string? Schema { get; }

    IEntityTypeBuilder ToTable(string tableName, string? schema = null);
    IEntityTypeBuilder HasNoKey();
    IEntityTypeBuilder IsReadOnly();
}
