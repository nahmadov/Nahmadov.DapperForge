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
    /// <param name="tableName">Table name.</param>
    /// <param name="schema">Optional schema name.</param>
    /// <returns>The builder instance.</returns>
    IEntityTypeBuilder ToTable(string tableName, string? schema = null);

    /// <summary>
    /// Marks the entity as keyless.
    /// </summary>
    /// <returns>The builder instance.</returns>
    IEntityTypeBuilder HasNoKey();

    /// <summary>
    /// Marks the entity as read-only.
    /// </summary>
    /// <returns>The builder instance.</returns>
    IEntityTypeBuilder IsReadOnly();
}
