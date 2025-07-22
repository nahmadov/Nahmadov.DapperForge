using System.Linq.Expressions;
using System.Reflection;

using Dapper;

using DapperToolkit.Core.Attributes;
using DapperToolkit.Core.Common;
using DapperToolkit.Core.Interfaces;
using DapperToolkit.Core.Mapping;
using DapperToolkit.Oracle.Common;

namespace DapperToolkit.Oracle.Context;

public class DapperDbSet<T> : IDapperDbSet<T>, IIncludableDbSet<T> where T : class
{
    private readonly DapperDbContext _context;
    private readonly string _tableName;
    public DapperDbSet(DapperDbContext context)
    {
        _context = context;

        var tableAttr = typeof(T).GetCustomAttribute<TableNameAttribute>();
        _tableName = tableAttr?.Name ?? typeof(T).Name;
        SqlMapper.SetTypeMap(typeof(T), new ColumnAttributeTypeMapper<T>());
    }

    public async Task<IEnumerable<T>> ToListAsync()
    {
        var sql = $"SELECT {DapperDbSet<T>.GetProjection()} FROM {_tableName}";
        return await _context.QueryAsync<T>(sql);
    }

    public async Task<IEnumerable<T>> ToListAsync(Expression<Func<T, object>> orderBy, bool ascending = true)
    {
        var orderByVisitor = new OracleOrderByVisitor();
        var orderByClause = orderByVisitor.TranslateOrderBy(orderBy, ascending);
        
        var sql = $"SELECT {DapperDbSet<T>.GetProjection()} FROM {_tableName} ORDER BY {orderByClause}";
        return await _context.QueryAsync<T>(sql);
    }

    public async Task<IEnumerable<T>> WhereAsync(Expression<Func<T, bool>> predicate)
    {
        var visitor = new OraclePredicateVisitor();
        var (whereClause, parameters) = visitor.Translate(predicate.Body);

        var sql = $"SELECT {DapperDbSet<T>.GetProjection()} FROM {_tableName} WHERE {whereClause}";
        return await _context.QueryAsync<T>(sql, parameters);
    }

    public async Task<IEnumerable<TResult>> SelectAsync<TResult>(Expression<Func<T, TResult>> selector)
    {
        var projectionVisitor = new OracleProjectionVisitor(typeof(T));
        var projection = projectionVisitor.TranslateProjection(selector);

        var sql = $"SELECT {projection} FROM {_tableName}";
        return await _context.QueryAsync<TResult>(sql);
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        var visitor = new OraclePredicateVisitor();
        var (whereClause, parameters) = visitor.Translate(predicate.Body);

        var sql = $"SELECT {DapperDbSet<T>.GetProjection()} FROM {_tableName} WHERE {whereClause}";
        return await _context.QueryFirstOrDefaultAsync<T>(sql, parameters);
    }

    public async Task<int> InsertAsync(T entity)
    {
        var properties = typeof(T).GetProperties()
            .Where(p => p.Name != "Id")
            .ToList();

        var columns = string.Join(", ", properties.Select(p =>
        {
            var attr = p.GetCustomAttribute<ColumnNameAttribute>();
            return attr?.Name ?? p.Name;
        }));

        var values = string.Join(", ", properties.Select(p => $":{p.Name}"));

        var sql = $"INSERT INTO {_tableName} ({columns}) VALUES ({values})";
        return await _context.ExecuteAsync(sql, entity);
    }

  public async Task<int> UpdateAsync(T entity)
  {
    var properties = typeof(T).GetProperties()
        .Where(p => p.Name != "Id")
        .ToList();

    var setClause = string.Join(", ", properties.Select(p =>
    {
      var attr = p.GetCustomAttribute<ColumnNameAttribute>();
      var columnName = attr?.Name ?? p.Name;
      return $"{columnName} = :{p.Name}";
    }));

    var idProperty = typeof(T).GetProperty("Id");
    if (idProperty == null)
        throw new InvalidOperationException("Entity must have an Id property for update.");

    var idAttr = idProperty.GetCustomAttribute<ColumnNameAttribute>();
    var idColumnName = idAttr?.Name ?? "Id";

    var sql = $"UPDATE {_tableName} SET {setClause} WHERE {idColumnName} = :Id";
    return await _context.ExecuteAsync(sql, entity);
  }

    public async Task<int> DeleteAsync(int id)
    {
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty == null)
            throw new InvalidOperationException("Entity must have an Id property for deletion.");

        var idAttr = idProperty.GetCustomAttribute<ColumnNameAttribute>();
        var idColumnName = idAttr?.Name ?? "Id";

        var sql = $"DELETE FROM {_tableName} WHERE {idColumnName} = :Id";
        return await _context.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<int> DeleteAsync(Expression<Func<T, bool>> predicate)
    {
        var visitor = new OraclePredicateVisitor();
        var (whereClause, parameters) = visitor.Translate(predicate.Body);

        var sql = $"DELETE FROM {_tableName} WHERE {whereClause}";
        return await _context.ExecuteAsync(sql, parameters);
    }

    public async Task<int> DeleteAsync(T entity)
    {
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty == null)
            throw new InvalidOperationException("Entity must have an Id property for deletion.");

        var idValue = idProperty.GetValue(entity);
        if (idValue == null)
            throw new InvalidOperationException("Entity Id cannot be null for deletion.");

        var idAttr = idProperty.GetCustomAttribute<ColumnNameAttribute>();
        var idColumnName = idAttr?.Name ?? "Id";

        var sql = $"DELETE FROM {_tableName} WHERE {idColumnName} = :Id";
        return await _context.ExecuteAsync(sql, new { Id = idValue });
    }

    public async Task<bool> AnyAsync()
    {
        var sql = $"SELECT CASE WHEN EXISTS(SELECT 1 FROM {_tableName}) THEN 1 ELSE 0 END FROM DUAL";
        var result = await _context.QueryFirstOrDefaultAsync<int>(sql);
        return result == 1;
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        var visitor = new OraclePredicateVisitor();
        var (whereClause, parameters) = visitor.Translate(predicate.Body);

        var sql = $"SELECT CASE WHEN EXISTS(SELECT 1 FROM {_tableName} WHERE {whereClause}) THEN 1 ELSE 0 END FROM DUAL";
        var result = await _context.QueryFirstOrDefaultAsync<int>(sql, parameters);
        return result == 1;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty == null)
            throw new InvalidOperationException("Entity must have an Id property for existence check.");

        var attr = idProperty.GetCustomAttribute<ColumnNameAttribute>();
        var columnName = attr?.Name ?? "Id";

        var sql = $"SELECT CASE WHEN EXISTS(SELECT 1 FROM {_tableName} WHERE {columnName} = :Id) THEN 1 ELSE 0 END FROM DUAL";
        var result = await _context.QueryFirstOrDefaultAsync<int>(sql, new { Id = id });
        return result == 1;
    }

    public async Task<int> CountAsync()
    {
        var sql = $"SELECT COUNT(*) FROM {_tableName}";
        return await _context.QueryFirstOrDefaultAsync<int>(sql);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
    {
        var visitor = new OraclePredicateVisitor();
        var (whereClause, parameters) = visitor.Translate(predicate.Body);

        var sql = $"SELECT COUNT(*) FROM {_tableName} WHERE {whereClause}";
        return await _context.QueryFirstOrDefaultAsync<int>(sql, parameters);
    }

    public async Task<IEnumerable<T>> PageAsync(int pageNumber, int pageSize)
    {
        if (pageNumber < 1) throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));
        if (pageSize < 1) throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));

        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty == null)
            throw new InvalidOperationException("Entity must have an Id property for pagination.");

        var idAttr = idProperty.GetCustomAttribute<ColumnNameAttribute>();
        var idColumnName = idAttr?.Name ?? "Id";

        var offset = (pageNumber - 1) * pageSize;
        var sql = $"SELECT {GetProjection()} FROM {_tableName} ORDER BY {idColumnName} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
        return await _context.QueryAsync<T>(sql);
    }

    public async Task<IEnumerable<T>> PageAsync(int pageNumber, int pageSize, Expression<Func<T, object>> orderBy, bool ascending = true)
    {
        if (pageNumber < 1) throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));
        if (pageSize < 1) throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));

        var orderByVisitor = new OracleOrderByVisitor();
        var orderByClause = orderByVisitor.TranslateOrderBy(orderBy, ascending);
        
        var offset = (pageNumber - 1) * pageSize;
        var sql = $"SELECT {GetProjection()} FROM {_tableName} ORDER BY {orderByClause} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
        return await _context.QueryAsync<T>(sql);
    }

    public async Task<(IEnumerable<T> Data, int TotalCount)> PageWithCountAsync(int pageNumber, int pageSize)
    {
        if (pageNumber < 1) throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));
        if (pageSize < 1) throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));

        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty == null)
            throw new InvalidOperationException("Entity must have an Id property for pagination.");

        var idAttr = idProperty.GetCustomAttribute<ColumnNameAttribute>();
        var idColumnName = idAttr?.Name ?? "Id";

        var offset = (pageNumber - 1) * pageSize;
        var sql = $@"
            SELECT {GetProjection()} FROM {_tableName} ORDER BY {idColumnName} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY;
            SELECT COUNT(*) FROM {_tableName};";
        
        using var multi = await _context.Connection.QueryMultipleAsync(sql);
        var data = await multi.ReadAsync<T>();
        var totalCount = await multi.ReadSingleAsync<int>();
        
        return (data, totalCount);
    }

    public async Task<(IEnumerable<T> Data, int TotalCount)> PageWithCountAsync(int pageNumber, int pageSize, Expression<Func<T, object>> orderBy, bool ascending = true)
    {
        if (pageNumber < 1) throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));
        if (pageSize < 1) throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));

        var orderByVisitor = new OracleOrderByVisitor();
        var orderByClause = orderByVisitor.TranslateOrderBy(orderBy, ascending);
        
        var offset = (pageNumber - 1) * pageSize;
        var sql = $@"
            SELECT {GetProjection()} FROM {_tableName} ORDER BY {orderByClause} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY;
            SELECT COUNT(*) FROM {_tableName};";
        
        using var multi = await _context.Connection.QueryMultipleAsync(sql);
        var data = await multi.ReadAsync<T>();
        var totalCount = await multi.ReadSingleAsync<int>();
        
        return (data, totalCount);
    }

    public async Task<IEnumerable<T>> IncludeAsync<TProperty>(Expression<Func<T, TProperty>> includeExpression)
    {
        var navigationInfo = NavigationPropertyAnalyzer.AnalyzeIncludeExpression(includeExpression);
        var visitor = new OracleIncludeVisitor(typeof(T), navigationInfo);
        var (sql, parameters) = visitor.GenerateIncludeQuery<T>();
        
        var result = await _context.Connection.QueryAsync(sql, parameters);
        return MapIncludeResults<TProperty>(result, navigationInfo);
    }

    public async Task<IEnumerable<T>> IncludeAsync<TProperty>(Expression<Func<T, bool>> predicate, Expression<Func<T, TProperty>> includeExpression)
    {
        var navigationInfo = NavigationPropertyAnalyzer.AnalyzeIncludeExpression(includeExpression);
        var visitor = new OracleIncludeVisitor(typeof(T), navigationInfo);
        var (sql, parameters) = visitor.GenerateIncludeQuery(predicate);
        
        var result = await _context.Connection.QueryAsync(sql, parameters);
        return MapIncludeResults<TProperty>(result, navigationInfo);
    }

    private IEnumerable<T> MapIncludeResults<TProperty>(IEnumerable<dynamic> rawResults, NavigationPropertyInfo navigationInfo)
    {
        var entities = new Dictionary<object, T>();
        
        foreach (var row in rawResults)
        {
            var entityData = new Dictionary<string, object>();
            var relatedData = new Dictionary<string, object>();
            
            foreach (var property in (row as IDictionary<string, object>)!)
            {
                if (property.Key.StartsWith("S_"))
                {
                    entityData[property.Key.Substring(2)] = property.Value;
                }
                else if (property.Key.StartsWith("T_"))
                {
                    relatedData[property.Key.Substring(2)] = property.Value;
                }
            }
            
            var entityKey = GetEntityKey(entityData);
            if (!entities.ContainsKey(entityKey))
            {
                var entity = MapDynamicToEntity<T>(entityData);
                entities[entityKey] = entity;
            }
            
            var relatedEntity = MapDynamicToEntity(relatedData, navigationInfo.TargetType);
            var navigationProperty = typeof(T).GetProperty(navigationInfo.PropertyName);
            
            if (navigationInfo.ForeignKeyInfo.IsCollection)
            {
                var collection = navigationProperty?.GetValue(entities[entityKey]) as System.Collections.IList;
                if (collection != null && !CollectionContains(collection, relatedEntity))
                {
                    collection.Add(relatedEntity);
                }
            }
            else
            {
                navigationProperty?.SetValue(entities[entityKey], relatedEntity);
            }
        }
        
        return entities.Values;
    }

    private object GetEntityKey(Dictionary<string, object> entityData)
    {
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty != null)
        {
            var columnAttr = idProperty.GetCustomAttribute<ColumnNameAttribute>();
            var columnName = columnAttr?.Name ?? "Id";
            return entityData.ContainsKey(columnName.ToUpper()) ? entityData[columnName.ToUpper()] : 0;
        }
        return 0;
    }

    private TEntity MapDynamicToEntity<TEntity>(Dictionary<string, object> data) where TEntity : class
    {
        return MapDynamicToEntity(data, typeof(TEntity)) as TEntity ?? throw new InvalidOperationException("Failed to map entity");
    }

    private object MapDynamicToEntity(Dictionary<string, object> data, Type entityType)
    {
        var entity = Activator.CreateInstance(entityType) ?? throw new InvalidOperationException($"Failed to create instance of {entityType.Name}");
        
        foreach (var property in entityType.GetProperties())
        {
            var columnAttr = property.GetCustomAttribute<ColumnNameAttribute>();
            var columnName = (columnAttr?.Name ?? property.Name).ToUpper();
            
            if (data.ContainsKey(columnName) && data[columnName] != DBNull.Value)
            {
                var value = Convert.ChangeType(data[columnName], property.PropertyType);
                property.SetValue(entity, value);
            }
        }
        
        return entity;
    }

    private bool CollectionContains(System.Collections.IList collection, object item)
    {
        foreach (var existingItem in collection)
        {
            if (AreEntitiesEqual(existingItem, item))
                return true;
        }
        return false;
    }

    private bool AreEntitiesEqual(object entity1, object entity2)
    {
        if (entity1 == null || entity2 == null) return false;
        if (entity1.GetType() != entity2.GetType()) return false;
        
        var idProperty = entity1.GetType().GetProperty("Id");
        if (idProperty != null)
        {
            var id1 = idProperty.GetValue(entity1);
            var id2 = idProperty.GetValue(entity2);
            return Equals(id1, id2);
        }
        
        return entity1.Equals(entity2);
    }

    public IIncludableQueryable<T, TProperty> Include<TProperty>(Expression<Func<T, TProperty>> includeExpression)
    {
        var include = new IncludeInfo
        {
            NavigationExpression = includeExpression,
            ParentType = typeof(T),
            PropertyType = typeof(TProperty),
            IsCollection = IsCollectionType(typeof(TProperty))
        };

        return new IncludableQueryable<T, TProperty>(this, new List<IncludeInfo> { include });
    }

    public IIncludableQueryable<T, TProperty> Include<TProperty>(Expression<Func<T, bool>> predicate, Expression<Func<T, TProperty>> includeExpression)
    {
        var include = new IncludeInfo
        {
            NavigationExpression = includeExpression,
            ParentType = typeof(T),
            PropertyType = typeof(TProperty),
            IsCollection = IsCollectionType(typeof(TProperty)),
            Predicate = predicate as Expression<Func<object, bool>>
        };

        return new IncludableQueryable<T, TProperty>(this, new List<IncludeInfo> { include });
    }

    public async Task<IEnumerable<T>> ExecuteWithIncludesAsync(List<IncludeInfo> includes)
    {
        if (includes.Count == 1)
        {
            var include = includes[0];
            if (include.Predicate != null)
            {
                var predicate = include.Predicate as Expression<Func<T, bool>>;
                var includeExpr = include.NavigationExpression as Expression<Func<T, object>>;
                if (predicate != null && includeExpr != null)
                {
                    return await IncludeAsync(predicate, includeExpr);
                }
            }
            else
            {
                var includeExpr = include.NavigationExpression as Expression<Func<T, object>>;
                if (includeExpr != null)
                {
                    return await IncludeAsync(includeExpr);
                }
            }
        }

        return await ExecuteMultiLevelIncludeAsync(includes);
    }

    private async Task<IEnumerable<T>> ExecuteMultiLevelIncludeAsync(List<IncludeInfo> includes)
    {
        var sql = GenerateMultiLevelJoinSql(includes);
        var result = await _context.Connection.QueryAsync(sql.Query, sql.Parameters);
        return MapMultiLevelResults(result, includes);
    }

    private (string Query, object Parameters) GenerateMultiLevelJoinSql(List<IncludeInfo> includes)
    {
        var firstInclude = includes[0];
        var navigationInfo = NavigationPropertyAnalyzer.AnalyzeIncludeExpression(firstInclude.NavigationExpression as dynamic);
        var visitor = new OracleIncludeVisitor(typeof(T), navigationInfo);
        return visitor.GenerateIncludeQuery<T>();
    }

    private IEnumerable<T> MapMultiLevelResults(IEnumerable<dynamic> results, List<IncludeInfo> includes)
    {
        var firstInclude = includes[0];
        var navigationInfo = NavigationPropertyAnalyzer.AnalyzeIncludeExpression(firstInclude.NavigationExpression as dynamic);
        return MapIncludeResults<object>(results, navigationInfo);
    }

    private bool IsCollectionType(Type type)
    {
        return type.IsGenericType &&
               (typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition()) ||
                type.GetGenericTypeDefinition() == typeof(List<>) ||
                type.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                type.GetGenericTypeDefinition() == typeof(IList<>));
    }

    private static string GetProjection()
    {
        var projection = "";
        typeof(T).GetProperties().ToList().ForEach(prop =>
        {
            var attr = prop.GetCustomAttribute<ColumnNameAttribute>();
            if (attr != null)
                projection += $"{attr.Name} AS {prop.Name}, ";
            else
                projection += $"{prop.Name}, ";
        });
        projection = projection.TrimEnd(',', ' ');
        return string.IsNullOrEmpty(projection.Trim()) ? "*" : projection;
    }
}
