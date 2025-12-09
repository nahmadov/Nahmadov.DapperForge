using Nahmadov.DapperForge.Core.Builders;

namespace Nahmadov.DapperForge.Core.Interfaces;

/// <summary>
/// Allows configuration of an entity type using a strongly-typed builder.
/// </summary>
public interface IEntityTypeConfiguration<TEntity>
{
    /// <summary>
    /// Applies configuration to the provided entity builder.
    /// </summary>
    /// <param name="builder">Builder used to configure the entity type.</param>
    void Configure(EntityTypeBuilder<TEntity> builder);
}
