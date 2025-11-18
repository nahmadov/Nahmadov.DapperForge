using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using DapperToolkit.Core.Attributes;

namespace DapperToolkit.Core.Common;

public abstract class BaseOrderByVisitor : ExpressionVisitor
{
    protected readonly StringBuilder _orderBy = new();
    protected bool _isFirst = true;

    public string TranslateOrderBy<T>(Expression<Func<T, object>> expression, bool ascending = true)
    {
        if (!_isFirst)
            _orderBy.Append(", ");
        
        Visit(expression.Body);
        _orderBy.Append(ascending ? " ASC" : " DESC");
        _isFirst = false;
        
        return _orderBy.ToString();
    }

    public void Reset()
    {
        _orderBy.Clear();
        _isFirst = true;
    }

    protected abstract string FormatColumn(string columnName);

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression != null && node.Expression.NodeType == ExpressionType.Parameter)
        {
            var columnAttr = node.Member.GetCustomAttribute<ColumnNameAttribute>();
            var columnName = columnAttr?.Name ?? node.Member.Name;
            _orderBy.Append(FormatColumn(columnName));
            return node;
        }

        throw new NotSupportedException("Only direct property access is supported in ORDER BY expressions.");
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType == ExpressionType.Convert)
        {
            return Visit(node.Operand);
        }
        
        throw new NotSupportedException($"Unary operator {node.NodeType} is not supported in ORDER BY expressions.");
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        throw new NotSupportedException("Constants are not supported in ORDER BY expressions.");
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        throw new NotSupportedException("Binary expressions are not supported in ORDER BY expressions.");
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        throw new NotSupportedException("Method calls are not supported in ORDER BY expressions.");
    }
}