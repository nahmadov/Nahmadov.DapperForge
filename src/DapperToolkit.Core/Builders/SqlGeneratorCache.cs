namespace DapperToolkit.Core.Builders;

internal static class SqlGeneratorCache<TEntity> where TEntity : class
{
    public static readonly SqlGenerator<TEntity> Instance = new();
}