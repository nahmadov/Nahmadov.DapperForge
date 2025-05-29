using System.Linq.Expressions;

using DapperToolkit.Core.Common;

namespace DapperToolkit.Oracle;

public class OraclePredicateVisitor : BasePredicateVisitor
{
    protected override string FormatParameter(string paramName) => $":{paramName}";
    protected override string FormatColumn(string columnName) => columnName;
    protected override string SqlOperator(ExpressionType type) => type switch
    {
        ExpressionType.Equal => "=",
        ExpressionType.NotEqual => "!=",
        ExpressionType.GreaterThan => ">",
        ExpressionType.GreaterThanOrEqual => ">=",
        ExpressionType.LessThan => "<",
        ExpressionType.LessThanOrEqual => "<=",
        ExpressionType.AndAlso => "AND",
        ExpressionType.OrElse => "OR",
        _ => throw new NotSupportedException($"Operator {type} not supported.")
    };
}