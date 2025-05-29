using System.Linq.Expressions;
using System.Text;

using Dapper;

namespace DapperToolkit.Core.Common;

public abstract class BasePredicateVisitor : ExpressionVisitor
{
    protected readonly StringBuilder _sql = new();
    protected readonly Dictionary<string, object> _parameters = [];
    protected int _paramIndex = 0;

    public (string Sql, DynamicParameters Parameters) Translate(Expression expression)
    {
        Visit(expression);
        return (_sql.ToString(), new DynamicParameters(_parameters));
    }

    protected string NextParam(object value)
    {
        var name = $"p{_paramIndex++}";
        _parameters[name] = value;
        return FormatParameter(name);
    }

    protected abstract string FormatParameter(string paramName);
    protected abstract string FormatColumn(string columnName);
    protected abstract string SqlOperator(ExpressionType type);

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _sql.Append('(');
        Visit(node.Left);
        _sql.Append($" {SqlOperator(node.NodeType)} ");
        Visit(node.Right);
        _sql.Append(')');
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression != null && node.Expression.NodeType == ExpressionType.Parameter)
        {
            _sql.Append(FormatColumn(node.Member.Name));
            return node;
        }

        var value = Expression.Lambda(node).Compile().DynamicInvoke();
        _sql.Append(NextParam(value!));
        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        _sql.Append(NextParam(node.Value!));
        return node;
    }
}