using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using DapperToolkit.Core.Attributes;

namespace DapperToolkit.Core.Common;

public abstract class BaseProjectionVisitor : ExpressionVisitor
{
    private readonly StringBuilder _projection = new();
    private readonly Type _sourceType;

    protected BaseProjectionVisitor(Type sourceType)
    {
        _sourceType = sourceType;
    }

    public string TranslateProjection<T, TResult>(Expression<Func<T, TResult>> selector)
    {
        _projection.Clear();
        
        if (selector.Body is NewExpression newExpr)
        {
            ProcessNewExpression(newExpr);
        }
        else if (selector.Body is MemberExpression memberExpr)
        {
            ProcessMemberExpression(memberExpr);
        }
        else
        {
            Visit(selector.Body);
        }
        
        var result = _projection.ToString().TrimEnd(',', ' ');
        return string.IsNullOrWhiteSpace(result) ? "*" : result;
    }

    private void ProcessNewExpression(NewExpression node)
    {
        if (node.Members != null)
        {
            for (int i = 0; i < node.Members.Count; i++)
            {
                if (node.Arguments[i] is MemberExpression memberExpression && 
                    memberExpression.Expression != null && 
                    memberExpression.Expression.Type == _sourceType)
                {
                    var property = memberExpression.Member as PropertyInfo;
                    if (property != null)
                    {
                        var columnAttr = property.GetCustomAttribute<ColumnNameAttribute>();
                        var columnName = columnAttr?.Name ?? property.Name;
                        var alias = node.Members[i].Name;
                        
                        _projection.Append($"{FormatColumn(columnName)} AS {FormatAlias(alias)}, ");
                    }
                }
            }
        }
    }

    private void ProcessMemberExpression(MemberExpression node)
    {
        if (node.Expression != null && node.Expression.Type == _sourceType)
        {
            var property = node.Member as PropertyInfo;
            if (property != null)
            {
                var columnAttr = property.GetCustomAttribute<ColumnNameAttribute>();
                var columnName = columnAttr?.Name ?? property.Name;
                
                _projection.Append($"{FormatColumn(columnName)}, ");
            }
        }
    }

    protected virtual string FormatColumn(string columnName) => columnName;
    protected virtual string FormatAlias(string alias) => alias;
}
