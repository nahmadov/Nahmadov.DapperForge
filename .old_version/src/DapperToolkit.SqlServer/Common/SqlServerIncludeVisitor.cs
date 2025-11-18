using System.Linq.Expressions;
using System.Reflection;
using DapperToolkit.Core.Attributes;
using DapperToolkit.Core.Common;
using DapperToolkit.SqlServer.Common;

namespace DapperToolkit.SqlServer.Common;

public class SqlServerIncludeVisitor : BaseIncludeVisitor
{
    private readonly SqlServerPredicateVisitor _predicateVisitor;

    public SqlServerIncludeVisitor(Type sourceType, NavigationPropertyInfo navigationInfo) 
        : base(sourceType, navigationInfo)
    {
        _predicateVisitor = new SqlServerPredicateVisitor();
    }

    protected override string GenerateWhereClause<T>(Expression<Func<T, bool>> predicate)
    {
        var (whereClause, _) = _predicateVisitor.Translate(predicate.Body);
        return whereClause;
    }

    protected override (string WhereClause, object Parameters) GenerateWhereClauseWithParameters<T>(Expression<Func<T, bool>> predicate)
    {
        var (whereClause, parameters) = _predicateVisitor.Translate(predicate.Body);
        
        // Add table prefix 's.' to column names to avoid ambiguity in JOINs
        whereClause = AddTablePrefixToColumns(whereClause, _sourceType);
        
        return (whereClause, parameters);
    }

    private string AddTablePrefixToColumns(string whereClause, Type sourceType)
    {
        var properties = sourceType.GetProperties();
        foreach (var property in properties)
        {
            var columnAttr = property.GetCustomAttribute<ColumnNameAttribute>();
            var columnName = columnAttr?.Name ?? property.Name;
            
            // Replace column name with prefixed version
            // Use word boundaries to avoid replacing partial matches
            whereClause = System.Text.RegularExpressions.Regex.Replace(
                whereClause, 
                $@"\b{System.Text.RegularExpressions.Regex.Escape(columnName)}\b", 
                $"s.[{columnName}]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        
        return whereClause;
    }

    protected override string FormatTableName(string tableName)
    {
        return $"[{tableName}]";
    }

    protected override string FormatColumnName(string columnName)
    {
        return $"[{columnName}]";
    }
}