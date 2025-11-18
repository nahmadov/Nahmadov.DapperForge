using System.Linq.Expressions;
using System.Reflection;
using DapperToolkit.Core.Common;
using DapperToolkit.Oracle.Common;

namespace DapperToolkit.Oracle.Common;

public class OracleMultiLevelIncludeVisitor : MultiLevelIncludeVisitor
{
    private readonly OraclePredicateVisitor _predicateVisitor;

    public OracleMultiLevelIncludeVisitor(Type rootType) : base(rootType)
    {
        _predicateVisitor = new OraclePredicateVisitor();
    }

    protected override (string WhereClause, object Parameters) GenerateWhereClauseWithParameters<T>(Expression<Func<T, bool>> predicate)
    {
        var (whereClause, parameters) = _predicateVisitor.Translate(predicate.Body);
        
        whereClause = AddTablePrefixToColumns(whereClause, _rootType, "r0");
        
        return (whereClause, parameters);
    }

    private string AddTablePrefixToColumns(string whereClause, Type entityType, string tableAlias)
    {
        var properties = entityType.GetProperties();
        foreach (var property in properties)
        {
            var columnAttr = property.GetCustomAttribute<DapperToolkit.Core.Attributes.ColumnNameAttribute>();
            var columnName = columnAttr?.Name ?? property.Name;
            
            whereClause = System.Text.RegularExpressions.Regex.Replace(
                whereClause, 
                $@"\b{System.Text.RegularExpressions.Regex.Escape(columnName)}\b", 
                $"{tableAlias}.{columnName}",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        
        return whereClause;
    }

    protected override string FormatTableName(string tableName)
    {
        return tableName;
    }

    protected override string FormatColumnName(string columnName)
    {
        return columnName;
    }
}