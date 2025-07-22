using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using DapperToolkit.Core.Attributes;

namespace DapperToolkit.Core.Common;

public abstract class BaseIncludeVisitor : ExpressionVisitor
{
    protected readonly StringBuilder _sql = new();
    protected readonly Type _sourceType;
    protected readonly NavigationPropertyInfo _navigationInfo;

    protected BaseIncludeVisitor(Type sourceType, NavigationPropertyInfo navigationInfo)
    {
        _sourceType = sourceType;
        _navigationInfo = navigationInfo;
    }

    public (string Sql, object Parameters) GenerateIncludeQuery<T>(Expression<Func<T, bool>>? predicate = null)
    {
        _sql.Clear();
        
        var sourceTableAttr = _sourceType.GetCustomAttribute<TableNameAttribute>();
        var sourceTableName = sourceTableAttr?.Name ?? _sourceType.Name;
        
        _sql.Append("SELECT ");
        _sql.Append(GenerateSelectClause());
        
        _sql.Append($" FROM {FormatTableName(sourceTableName)} s");
        _sql.Append($" INNER JOIN {FormatTableName(_navigationInfo.JoinInfo.TargetTable)} t");
        _sql.Append($" ON s.{FormatColumnName(_navigationInfo.JoinInfo.SourceForeignKeyColumn)} = t.{FormatColumnName(_navigationInfo.JoinInfo.TargetPrimaryKeyColumn)}");
        
        object parameters = new { };
        
        if (predicate != null)
        {
            var (whereClause, whereParameters) = GenerateWhereClauseWithParameters(predicate);
            if (!string.IsNullOrWhiteSpace(whereClause))
            {
                _sql.Append($" WHERE {whereClause}");
                parameters = whereParameters;
            }
        }
        
        return (_sql.ToString(), parameters);
    }

    private string GenerateSelectClause()
    {
        var selectClause = new StringBuilder();
        
        var sourceProperties = _sourceType.GetProperties()
            .Where(p => p.Name != _navigationInfo.PropertyName); 
            
        foreach (var prop in sourceProperties)
        {
            var columnAttr = prop.GetCustomAttribute<ColumnNameAttribute>();
            var columnName = columnAttr?.Name ?? prop.Name;
            selectClause.Append($"s.{FormatColumnName(columnName)} AS s_{columnName}, ");
        }
        
        var targetProperties = _navigationInfo.TargetType.GetProperties()
            .Where(p => !IsNavigationProperty(p));
            
        foreach (var prop in targetProperties)
        {
            var columnAttr = prop.GetCustomAttribute<ColumnNameAttribute>();
            var columnName = columnAttr?.Name ?? prop.Name;
            selectClause.Append($"t.{FormatColumnName(columnName)} AS t_{columnName}, ");
        }
        
        return selectClause.ToString().TrimEnd(',', ' ');
    }

    private bool IsNavigationProperty(PropertyInfo property)
    {
        return property.GetCustomAttribute<ForeignKeyAttribute>() != null ||
               property.GetCustomAttribute<InversePropertyAttribute>() != null ||
               (!property.PropertyType.IsPrimitive && 
                property.PropertyType != typeof(string) && 
                property.PropertyType != typeof(DateTime) && 
                property.PropertyType != typeof(decimal) && 
                property.PropertyType != typeof(Guid) &&
                !property.PropertyType.IsValueType);
    }

    protected abstract string GenerateWhereClause<T>(Expression<Func<T, bool>> predicate);
    protected abstract (string WhereClause, object Parameters) GenerateWhereClauseWithParameters<T>(Expression<Func<T, bool>> predicate);
    protected abstract string FormatTableName(string tableName);
    protected abstract string FormatColumnName(string columnName);
}
