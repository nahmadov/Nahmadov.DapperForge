namespace Nahmadov.DapperForge.Core.Builders;

/// <summary>
/// Non-generic abstraction for configuring entity mappings at runtime.
/// </summary>
public interface IEntityTypeBuilder
{
    string? TableName { get; }
    string? Schema { get; }

    /// <summary>
    /// Configures the database table and optional schema for the entity.
    /// </summary>
    IEntityTypeBuilder ToTable(string tableName, string? schema = null);

    /// <summary>
    /// Marks the entity as keyless.
    /// </summary>
    IEntityTypeBuilder HasNoKey();

    /// <summary>
    /// Marks the entity as read-only.
    /// </summary>
    IEntityTypeBuilder IsReadOnly();
}
