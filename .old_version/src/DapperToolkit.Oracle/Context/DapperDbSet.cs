using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

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

    protected override async Task<int> ExecuteBulkInsertAsync(List<T> entities, IDbTransaction? transaction)
    {
        var properties = PrimaryKeyHelper.GetNonPrimaryKeyProperties(typeof(T)).ToList();
        
        if (!properties.Any())
            return 0;

        var columns = string.Join(", ", properties.Select(p =>
        {
            var attr = p.GetCustomAttribute<ColumnNameAttribute>();
            return attr?.Name ?? p.Name;
        }));

        var sql = new StringBuilder("INSERT ALL");
        var parameters = new Dictionary<string, object>();
        
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            var values = new List<string>();
            
            foreach (var property in properties)
            {
                var paramName = $"{property.Name}_{i}";
                values.Add($":{paramName}");
                parameters[paramName] = property.GetValue(entity) ?? DBNull.Value;
            }
            
            sql.AppendLine($" INTO {FormatTableName(_tableName)} ({columns}) VALUES ({string.Join(", ", values)})");
        }
        
        sql.AppendLine(" SELECT * FROM DUAL");

        var connection = transaction?.Connection ?? _context.Connection;
        return await connection.ExecuteAsync(sql.ToString(), parameters, transaction);
    }

    protected override async Task<int> ExecuteBulkUpdateAsync(List<T> entities, IDbTransaction? transaction)
    {
        var properties = PrimaryKeyHelper.GetNonPrimaryKeyProperties(typeof(T)).ToList();
        var primaryKeyColumn = PrimaryKeyHelper.GetPrimaryKeyColumnName(typeof(T))!;
        var primaryKeyProperty = PrimaryKeyHelper.GetPrimaryKeyPropertyName(typeof(T))!;
        
        if (!properties.Any())
            return 0;

        var sql = new StringBuilder();
        sql.AppendLine("BEGIN");
        
        var parameters = new Dictionary<string, object>();
        
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            var setClauses = new List<string>();
            
            foreach (var property in properties)
            {
                var attr = property.GetCustomAttribute<ColumnNameAttribute>();
                var columnName = attr?.Name ?? property.Name;
                var paramName = $"{property.Name}_{i}";
                
                setClauses.Add($"{columnName} = :{paramName}");
                parameters[paramName] = property.GetValue(entity) ?? DBNull.Value;
            }
            
            var primaryKeyParamName = $"{primaryKeyProperty}_{i}";
            parameters[primaryKeyParamName] = PrimaryKeyHelper.GetPrimaryKeyValue(entity)!;
            
            sql.AppendLine($"  UPDATE {FormatTableName(_tableName)} SET {string.Join(", ", setClauses)} WHERE {primaryKeyColumn} = :{primaryKeyParamName};");
        }
        
        sql.AppendLine("END;");

        var connection = transaction?.Connection ?? _context.Connection;
        return await connection.ExecuteAsync(sql.ToString(), parameters, transaction);
    }

    protected override async Task<int> ExecuteBulkDeleteAsync(List<T> entities, IDbTransaction? transaction)
    {
        var ids = entities.Select(e => PrimaryKeyHelper.GetPrimaryKeyValue(e)).ToList();
        return await ExecuteBulkDeleteByIdsAsync(ids.Cast<int>().ToList(), transaction);
    }

    protected override async Task<int> ExecuteBulkDeleteByIdsAsync(List<int> ids, IDbTransaction? transaction)
    {
        var primaryKeyColumn = PrimaryKeyHelper.GetPrimaryKeyColumnName(typeof(T))!;
        
        var batchSize = 1000;
        var totalAffected = 0;
        
        for (int i = 0; i < ids.Count; i += batchSize)
        {
            var batch = ids.Skip(i).Take(batchSize).ToList();
            var parameterNames = batch.Select((id, index) => $":id_{i + index}").ToArray();
            var parameters = new Dictionary<string, object>();
            
            for (int j = 0; j < batch.Count; j++)
            {
                parameters[$"id_{i + j}"] = batch[j];
            }
            
            var sql = $"DELETE FROM {FormatTableName(_tableName)} WHERE {primaryKeyColumn} IN ({string.Join(", ", parameterNames)})";
            var connection = transaction?.Connection ?? _context.Connection;
            
            totalAffected += await connection.ExecuteAsync(sql, parameters, transaction);
        }
        
        return totalAffected;
    }
}
