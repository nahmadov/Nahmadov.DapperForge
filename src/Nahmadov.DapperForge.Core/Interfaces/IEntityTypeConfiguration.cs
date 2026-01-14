using Nahmadov.DapperForge.Core.Builders;

namespace Nahmadov.DapperForge.Core.Interfaces;

/// <summary>
/// Allows configuration of an entity type using a strongly-typed builder.
/// </summary>
public interface IEntityTypeConfiguration<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Applies configuration to the provided entity builder.
    /// </summary>
    void Configure(EntityTypeBuilder<TEntity> builder);
}
