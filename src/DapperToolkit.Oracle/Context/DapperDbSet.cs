using System.Linq.Expressions;
using System.Reflection;

using Dapper;

using DapperToolkit.Core.Attributes;
using DapperToolkit.Core.Common;
using DapperToolkit.Core.Interfaces;
using DapperToolkit.Core.Mapping;
using DapperToolkit.Oracle.Common;

namespace DapperToolkit.Oracle.Context;

public class DapperDbSet<T> : BaseDapperDbSet<T> where T : class
{
    public DapperDbSet(DapperDbContext context) : base(context)
    {
    }


    protected override string FormatParameter(string parameterName) => $":{parameterName}";
    
    protected override string FormatTableName(string tableName) => tableName;
    
    protected override string GenerateExistsQuery(string subQuery) => $"SELECT CASE WHEN EXISTS({subQuery}) THEN 1 ELSE 0 END FROM DUAL";
    
    protected override string GetSourcePrefix() => "S_";
    
    protected override string GetTargetPrefix() => "T_";
    
    protected override string GetEntityKeyColumnName(string columnName) => columnName.ToUpper();
    
    protected override string GetMappingColumnName(string columnName) => columnName.ToUpper();
    
    protected override BasePredicateVisitor CreatePredicateVisitor() => new OraclePredicateVisitor();
    
    protected override BaseOrderByVisitor CreateOrderByVisitor() => new OracleOrderByVisitor();
    
    protected override BaseProjectionVisitor CreateProjectionVisitor(Type entityType) => new OracleProjectionVisitor(entityType);
    
    protected override BaseIncludeVisitor CreateIncludeVisitor(Type entityType, NavigationPropertyInfo navigationInfo) => new OracleIncludeVisitor(entityType, navigationInfo);
    
    protected override MultiLevelIncludeVisitor CreateMultiLevelIncludeVisitor(Type rootType) => new OracleMultiLevelIncludeVisitor(rootType);
}
