namespace DapperToolkit.Core.Builders;

public sealed class FilterExpression<TEntity>(string sql, object parameters)
{
    public string Sql { get; } = sql;
    public object Parameters { get; } = parameters;
}