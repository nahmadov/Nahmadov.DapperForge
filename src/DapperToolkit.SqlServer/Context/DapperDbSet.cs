using System.Linq.Expressions;
using System.Reflection;

using Dapper;

using DapperToolkit.Core.Attributes;
using DapperToolkit.Core.Common;
using DapperToolkit.Core.Interfaces;
using DapperToolkit.Core.Mapping;
using DapperToolkit.SqlServer.Common;

namespace DapperToolkit.SqlServer.Context;

public class DapperDbSet<T> : BaseDapperDbSet<T> where T : class
{
    public DapperDbSet(DapperDbContext context) : base(context)
    {
    }


    protected override string FormatParameter(string parameterName) => $"@{parameterName}";
    
    protected override string FormatTableName(string tableName) => tableName;
    
    protected override string GenerateExistsQuery(string subQuery) => $"SELECT CASE WHEN EXISTS({subQuery}) THEN 1 ELSE 0 END";
    
    protected override string GetSourcePrefix() => "s_";
    
    protected override string GetTargetPrefix() => "t_";
    
    protected override string GetEntityKeyColumnName(string columnName) => columnName;
    
    protected override string GetMappingColumnName(string columnName) => columnName;
    
    protected override BasePredicateVisitor CreatePredicateVisitor() => new SqlServerPredicateVisitor();
    
    protected override BaseOrderByVisitor CreateOrderByVisitor() => new SqlServerOrderByVisitor();
    
    protected override BaseProjectionVisitor CreateProjectionVisitor(Type entityType) => new SqlServerProjectionVisitor(entityType);
    
    protected override BaseIncludeVisitor CreateIncludeVisitor(Type entityType, NavigationPropertyInfo navigationInfo) => new SqlServerIncludeVisitor(entityType, navigationInfo);
    
    protected override MultiLevelIncludeVisitor CreateMultiLevelIncludeVisitor(Type rootType) => new SqlServerMultiLevelIncludeVisitor(rootType);
}
