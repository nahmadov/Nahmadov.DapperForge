using DapperToolkit.Core.Builders;

namespace DapperToolkit.Core.Interfaces;

public interface IEntityTypeConfiguration<TEntity>
{
    void Configure(EntityTypeBuilder<TEntity> builder);
}