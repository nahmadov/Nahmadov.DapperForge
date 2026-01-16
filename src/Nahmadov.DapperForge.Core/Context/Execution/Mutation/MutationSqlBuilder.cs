using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Exceptions;
using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Context.Execution.Mutation;

/// <summary>
/// Builds SQL statements for mutation operations.
/// </summary>
internal sealed class MutationSqlBuilder<TEntity>(SqlGenerator<TEntity> generator, EntityMapping mapping) where TEntity : class
{
    private readonly SqlGenerator<TEntity> _generator = generator;
    private readonly EntityMapping _mapping = mapping;

    public string BuildUpdateSqlWithWhere(string whereClause)
    {
        if (string.IsNullOrWhiteSpace(_generator.UpdateSql))
        {
            throw new DapperConfigurationException(
                typeof(TEntity).Name,
                "Update SQL is not configured or no columns are updatable.");
        }

        var sqlBeforeWhere = _generator.UpdateSql.Substring(
            0,
            _generator.UpdateSql.LastIndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase));

        return $"{sqlBeforeWhere} WHERE {whereClause}";
    }

    public string BuildDeleteSql(string whereClause)
    {
        var tableName = BuildFullTableName();
        return $"DELETE FROM {tableName} WHERE {whereClause}";
    }

    public string BuildCountSql(string whereClause)
    {
        var tableName = BuildFullTableName();
        return $"SELECT COUNT(*) FROM {tableName} WHERE {whereClause}";
    }

    private string BuildFullTableName()
    {
        return string.IsNullOrWhiteSpace(_mapping.Schema)
            ? _generator.Dialect.QuoteIdentifier(_mapping.TableName)
            : $"{_generator.Dialect.QuoteIdentifier(_mapping.Schema)}.{_generator.Dialect.QuoteIdentifier(_mapping.TableName)}";
    }
}
