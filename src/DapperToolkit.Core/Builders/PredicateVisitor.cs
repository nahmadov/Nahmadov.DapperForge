using System.Linq.Expressions;
using System.Text;

namespace DapperToolkit.Core.Builders;

public sealed class PredicateVisitor : ExpressionVisitor
{
    private readonly StringBuilder _sb = new();
    private readonly Dictionary<string, object> _params = new();
    private int _index = 0;

    public (string sql, object parameters) Translate(Expression exp)
    {
        Visit(exp);
        return (_sb.ToString(), _params.ToDictionary(x => x.Key, x => x.Value));
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _sb.Append('(');
        Visit(node.Left);

        _sb.Append(node.NodeType switch
        {
            ExpressionType.Equal => " = ",
            ExpressionType.GreaterThan => " > ",
            ExpressionType.LessThan => " < ",
            ExpressionType.AndAlso => " AND ",
            ExpressionType.OrElse => " OR ",
            _ => throw new NotSupportedException($"Unsupported node: {node.NodeType}")
        });

        Visit(node.Right);
        _sb.Append(')');
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        _sb.Append(node.Member.Name);
        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        var paramName = $"p{_index++}";
        _params[paramName] = node.Value!;
        _sb.Append($"@{paramName}");
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // supports: x.Name.Contains("abc")
        if (node.Method.Name == "Contains")
        {
            Visit(node.Object);
            _sb.Append(" LIKE ");
            var value = ((ConstantExpression)node.Arguments[0]).Value!;
            var paramName = $"p{_index++}";
            _params[paramName] = $"%{value}%";
            _sb.Append($"@{paramName}");
            return node;
        }

        throw new NotSupportedException($"Method not supported: {node.Method.Name}");
    }
}