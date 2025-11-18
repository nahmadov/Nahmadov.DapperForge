using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using DapperToolkit.Core.Attributes;

namespace DapperToolkit.Core.Common;

public abstract class MultiLevelIncludeVisitor : ExpressionVisitor
{
    protected readonly StringBuilder _sql = new();
    protected readonly Type _rootType;
    protected readonly List<IncludeLevel> _includeLevels = new();

    protected MultiLevelIncludeVisitor(Type rootType)
    {
        _rootType = rootType;
    }

    public (string Sql, object Parameters) GenerateMultiLevelIncludeQuery<T>(List<IncludeInfo> includes, Expression<Func<T, bool>>? predicate = null)
    {
        _sql.Clear();
        _includeLevels.Clear();

        BuildIncludeLevels(includes);

        _sql.Append("SELECT ");
        _sql.Append(GenerateMultiLevelSelectClause());

        _sql.Append($" FROM {FormatTableName(GetTableName(_rootType))} r0");
        GenerateJoinClauses();

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

    public List<IncludeLevel> GetIncludeLevels() => _includeLevels;

    private void BuildIncludeLevels(List<IncludeInfo> includes)
    {
        var currentType = _rootType;
        var currentAlias = "r0";
        var level = 0;

        _includeLevels.Add(new IncludeLevel
        {
            Level = level,
            Type = currentType,
            TableName = GetTableName(currentType),
            Alias = currentAlias,
            IsRoot = true
        });

        foreach (var include in includes)
        {
            level++;
            var navigationInfo = NavigationPropertyAnalyzer.AnalyzeIncludeExpression(include.NavigationExpression as dynamic);
            var nextAlias = $"t{level}";

            var includeLevel = new IncludeLevel
            {
                Level = level,
                Type = navigationInfo.TargetType,
                TableName = navigationInfo.JoinInfo.TargetTable,
                Alias = nextAlias,
                NavigationInfo = navigationInfo,
                ParentAlias = currentAlias,
                IsCollection = navigationInfo.ForeignKeyInfo.IsCollection
            };

            _includeLevels.Add(includeLevel);

            currentType = navigationInfo.TargetType;
            currentAlias = nextAlias;
        }
    }

    private string GenerateMultiLevelSelectClause()
    {
        var selectClause = new StringBuilder();

        foreach (var level in _includeLevels)
        {
            var properties = level.Type.GetProperties()
                .Where(p => !IsNavigationProperty(p));

            foreach (var prop in properties)
            {
                var columnAttr = prop.GetCustomAttribute<ColumnNameAttribute>();
                var columnName = columnAttr?.Name ?? prop.Name;
                selectClause.Append($"{level.Alias}.{FormatColumnName(columnName)} AS {level.Alias}_{columnName}, ");
            }
        }

        return selectClause.ToString().TrimEnd(',', ' ');
    }

    private void GenerateJoinClauses()
    {
        for (int i = 1; i < _includeLevels.Count; i++)
        {
            var level = _includeLevels[i];
            var parentLevel = _includeLevels[i - 1];

            _sql.Append($" INNER JOIN {FormatTableName(level.TableName)} {level.Alias}");
            
            if (level.NavigationInfo!.ForeignKeyInfo.IsCollection)
            {
                var parentIdColumn = GetPrimaryKeyColumn(parentLevel.Type);
                var childFkColumn = level.NavigationInfo.ForeignKeyInfo.ForeignKeyColumnName;
                _sql.Append($" ON {parentLevel.Alias}.{FormatColumnName(parentIdColumn)} = {level.Alias}.{FormatColumnName(childFkColumn)}");
            }
            else
            {
                var childFkColumn = level.NavigationInfo.ForeignKeyInfo.ForeignKeyColumnName;
                var parentIdColumn = GetPrimaryKeyColumn(level.Type);
                _sql.Append($" ON {parentLevel.Alias}.{FormatColumnName(childFkColumn)} = {level.Alias}.{FormatColumnName(parentIdColumn)}");
            }
        }
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

    private string GetTableName(Type type)
    {
        var tableAttr = type.GetCustomAttribute<TableNameAttribute>();
        return tableAttr?.Name ?? type.Name;
    }

    private string GetPrimaryKeyColumn(Type type)
    {
        var primaryKeyColumnName = PrimaryKeyHelper.GetPrimaryKeyColumnName(type);
        if (primaryKeyColumnName != null)
            return primaryKeyColumnName;

        var firstIntProperty = type.GetProperties()
            .FirstOrDefault(p => p.PropertyType == typeof(int) || p.PropertyType == typeof(int?));

        if (firstIntProperty != null)
        {
            var columnAttr = firstIntProperty.GetCustomAttribute<ColumnNameAttribute>();
            return columnAttr?.Name ?? firstIntProperty.Name;
        }

        throw new InvalidOperationException($"Could not determine primary key for type '{type.Name}'.");
    }

    protected abstract (string WhereClause, object Parameters) GenerateWhereClauseWithParameters<T>(Expression<Func<T, bool>> predicate);
    protected abstract string FormatTableName(string tableName);
    protected abstract string FormatColumnName(string columnName);
}

public class IncludeLevel
{
    public int Level { get; set; }
    public Type Type { get; set; } = null!;
    public string TableName { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public NavigationPropertyInfo? NavigationInfo { get; set; }
    public string ParentAlias { get; set; } = string.Empty;
    public bool IsRoot { get; set; }
    public bool IsCollection { get; set; }
}
