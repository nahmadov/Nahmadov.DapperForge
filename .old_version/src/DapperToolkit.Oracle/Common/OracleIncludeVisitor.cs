using System.Linq.Expressions;
using System.Reflection;
using DapperToolkit.Core.Attributes;
using DapperToolkit.Core.Common;
using DapperToolkit.Oracle.Common;

namespace DapperToolkit.Oracle.Common;

public class OracleIncludeVisitor : BaseIncludeVisitor
{
    private readonly OraclePredicateVisitor _predicateVisitor;

    public OracleIncludeVisitor(Type sourceType, NavigationPropertyInfo navigationInfo) 
        : base(sourceType, navigationInfo)
    {
        _predicateVisitor = new OraclePredicateVisitor();
    }

    protected override string GenerateWhereClause<T>(Expression<Func<T, bool>> predicate)
    {
        var (whereClause, _) = _predicateVisitor.Translate(predicate.Body);
        return whereClause;
    }

    protected override (string WhereClause, object Parameters) GenerateWhereClauseWithParameters<T>(Expression<Func<T, bool>> predicate)
    {
        var (whereClause, parameters) = _predicateVisitor.Translate(predicate.Body);
        
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
            

            whereClause = System.Text.RegularExpressions.Regex.Replace(
                whereClause, 
                $@"\b{System.Text.RegularExpressions.Regex.Escape(columnName.ToUpper())}\b", 
                $"S.{columnName.ToUpper()}",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        
        return whereClause;
    }

    protected override string FormatTableName(string tableName)
    {
        return tableName.ToUpper();
    }

    protected override string FormatColumnName(string columnName)
    {
        return columnName.ToUpper();
    }
}
