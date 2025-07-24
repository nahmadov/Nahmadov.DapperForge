using System.Data;
using System.Linq.Expressions;
using System.Reflection;

using Dapper;

using DapperToolkit.Core.Attributes;
using DapperToolkit.Core.Interfaces;
using DapperToolkit.Core.Mapping;

namespace DapperToolkit.Core.Common;

public abstract class BaseDapperDbSet<T> : IDapperDbSet<T>, IIncludableDbSet<T> where T : class
{
    protected readonly IDapperDbContext _context;
    protected readonly string _tableName;

    protected BaseDapperDbSet(IDapperDbContext context)
    {
        _context = context;

        var tableAttr = typeof(T).GetCustomAttribute<TableNameAttribute>();
        _tableName = tableAttr?.Name ?? typeof(T).Name;
        SqlMapper.SetTypeMap(typeof(T), new ColumnAttributeTypeMapper<T>());
    }

    public async Task<IEnumerable<T>> ToListAsync()
    {
        var sql = $"SELECT {GetProjection()} FROM {FormatTableName(_tableName)}";
        return await _context.QueryAsync<T>(sql);
    }

    public async Task<IEnumerable<T>> ToListAsync(Expression<Func<T, object>> orderBy, bool ascending = true)
    {
        ValidationHelper.ValidateExpression(orderBy, nameof(orderBy));
        
        var orderByVisitor = CreateOrderByVisitor();
        var orderByClause = orderByVisitor.TranslateOrderBy(orderBy, ascending);
        
        var sql = $"SELECT {GetProjection()} FROM {FormatTableName(_tableName)} ORDER BY {orderByClause}";
        return await _context.QueryAsync<T>(sql);
    }

    public async Task<IEnumerable<T>> WhereAsync(Expression<Func<T, bool>> predicate)
    {
        ValidationHelper.ValidateExpression(predicate, nameof(predicate));
        
        var visitor = CreatePredicateVisitor();
        var (whereClause, parameters) = visitor.Translate(predicate.Body);

        var sql = $"SELECT {GetProjection()} FROM {FormatTableName(_tableName)} WHERE {whereClause}";
        return await _context.QueryAsync<T>(sql, parameters);
    }

    public async Task<IEnumerable<TResult>> SelectAsync<TResult>(Expression<Func<T, TResult>> selector)
    {
        ValidationHelper.ValidateExpression(selector, nameof(selector));
        
        var projectionVisitor = CreateProjectionVisitor(typeof(T));
        var projection = projectionVisitor.TranslateProjection<T, TResult>(selector);

        var sql = $"SELECT {projection} FROM {FormatTableName(_tableName)}";
        return await _context.QueryAsync<TResult>(sql);
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        ValidationHelper.ValidateExpression(predicate, nameof(predicate));
        
        var visitor = CreatePredicateVisitor();
        var (whereClause, parameters) = visitor.Translate(predicate.Body);

        var sql = $"SELECT {GetProjection()} FROM {FormatTableName(_tableName)} WHERE {whereClause}";
        return await _context.QueryFirstOrDefaultAsync<T>(sql, parameters);
    }

    public async Task<int> InsertAsync(T entity, IDbTransaction? transaction = null)
    {
        ValidationHelper.ValidateEntity(entity, "insert");
        var properties = PrimaryKeyHelper.GetNonPrimaryKeyProperties(typeof(T)).ToList();

        var columns = string.Join(", ", properties.Select(p =>
        {
            var attr = p.GetCustomAttribute<ColumnNameAttribute>();
            return attr?.Name ?? p.Name;
        }));

        var values = string.Join(", ", properties.Select(p => FormatParameter(p.Name)));

        var sql = $"INSERT INTO {FormatTableName(_tableName)} ({columns}) VALUES ({values})";
        return await _context.ExecuteAsync(sql, entity, transaction);
    }

    public async Task<int> UpdateAsync(T entity, IDbTransaction? transaction = null)
    {
        ValidationHelper.ValidateEntity(entity, "update");
        
        var properties = PrimaryKeyHelper.GetNonPrimaryKeyProperties(typeof(T)).ToList();

        var setClause = string.Join(", ", properties.Select(p =>
        {
            var attr = p.GetCustomAttribute<ColumnNameAttribute>();
            var columnName = attr?.Name ?? p.Name;
            return $"{columnName} = {FormatParameter(p.Name)}";
        }));

        PrimaryKeyHelper.ValidatePrimaryKey(typeof(T), "update");
        var idColumnName = PrimaryKeyHelper.GetPrimaryKeyColumnName(typeof(T))!;

        var primaryKeyPropertyName = PrimaryKeyHelper.GetPrimaryKeyPropertyName(typeof(T))!;
        var sql = $"UPDATE {FormatTableName(_tableName)} SET {setClause} WHERE {idColumnName} = {FormatParameter(primaryKeyPropertyName)}";
        return await _context.ExecuteAsync(sql, entity, transaction);
    }

    public async Task<int> DeleteAsync(int id, IDbTransaction? transaction = null)
    {
        PrimaryKeyHelper.ValidatePrimaryKey(typeof(T), "deletion");
        var idColumnName = PrimaryKeyHelper.GetPrimaryKeyColumnName(typeof(T))!;
        var primaryKeyPropertyName = PrimaryKeyHelper.GetPrimaryKeyPropertyName(typeof(T))!;

        var sql = $"DELETE FROM {FormatTableName(_tableName)} WHERE {idColumnName} = {FormatParameter(primaryKeyPropertyName)}";
        var parameters = new Dictionary<string, object> { [primaryKeyPropertyName] = id };
        return await _context.ExecuteAsync(sql, parameters, transaction);
    }

    public async Task<int> DeleteAsync(Expression<Func<T, bool>> predicate, IDbTransaction? transaction = null)
    {
        ValidationHelper.ValidateExpression(predicate, nameof(predicate));
        
        var visitor = CreatePredicateVisitor();
        var (whereClause, parameters) = visitor.Translate(predicate.Body);

        var sql = $"DELETE FROM {FormatTableName(_tableName)} WHERE {whereClause}";
        return await _context.ExecuteAsync(sql, parameters, transaction);
    }

    public async Task<int> DeleteAsync(T entity, IDbTransaction? transaction = null)
    {
        ValidationHelper.ValidateEntity(entity, "deletion");
        PrimaryKeyHelper.ValidatePrimaryKey(typeof(T), "deletion");
        PrimaryKeyHelper.ValidatePrimaryKeyValue(entity, "deletion");
        
        var idColumnName = PrimaryKeyHelper.GetPrimaryKeyColumnName(typeof(T))!;
        var primaryKeyPropertyName = PrimaryKeyHelper.GetPrimaryKeyPropertyName(typeof(T))!;
        var idValue = PrimaryKeyHelper.GetPrimaryKeyValue(entity);

        var sql = $"DELETE FROM {FormatTableName(_tableName)} WHERE {idColumnName} = {FormatParameter(primaryKeyPropertyName)}";
        var parameters = new Dictionary<string, object> { [primaryKeyPropertyName] = idValue! };
        return await _context.ExecuteAsync(sql, parameters, transaction);
    }

    public async Task ExecuteInTransactionAsync(Func<IDbTransaction, Task> operation)
    {
        using var transaction = await _context.BeginTransactionAsync();
        try
        {
            await operation(transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<IDbTransaction, Task<TResult>> operation)
    {
        using var transaction = await _context.BeginTransactionAsync();
        try
        {
            var result = await operation(transaction);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> AnyAsync()
    {
        var sql = GenerateExistsQuery($"SELECT 1 FROM {FormatTableName(_tableName)}");
        var result = await _context.QueryFirstOrDefaultAsync<int>(sql);
        return result == 1;
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        var visitor = CreatePredicateVisitor();
        var (whereClause, parameters) = visitor.Translate(predicate.Body);

        var sql = GenerateExistsQuery($"SELECT 1 FROM {FormatTableName(_tableName)} WHERE {whereClause}");
        var result = await _context.QueryFirstOrDefaultAsync<int>(sql, parameters);
        return result == 1;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        PrimaryKeyHelper.ValidatePrimaryKey(typeof(T), "existence check");
        var columnName = PrimaryKeyHelper.GetPrimaryKeyColumnName(typeof(T))!;
        var primaryKeyPropertyName = PrimaryKeyHelper.GetPrimaryKeyPropertyName(typeof(T))!;

        var sql = GenerateExistsQuery($"SELECT 1 FROM {FormatTableName(_tableName)} WHERE {columnName} = {FormatParameter(primaryKeyPropertyName)}");
        var parameters = new Dictionary<string, object> { [primaryKeyPropertyName] = id };
        var result = await _context.QueryFirstOrDefaultAsync<int>(sql, parameters);
        return result == 1;
    }

    public async Task<int> CountAsync()
    {
        var sql = $"SELECT COUNT(*) FROM {FormatTableName(_tableName)}";
        return await _context.QueryFirstOrDefaultAsync<int>(sql);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
    {
        var visitor = CreatePredicateVisitor();
        var (whereClause, parameters) = visitor.Translate(predicate.Body);

        var sql = $"SELECT COUNT(*) FROM {FormatTableName(_tableName)} WHERE {whereClause}";
        return await _context.QueryFirstOrDefaultAsync<int>(sql, parameters);
    }

    public async Task<IEnumerable<T>> PageAsync(int pageNumber, int pageSize)
    {
        ValidationHelper.ValidatePagination(pageNumber, pageSize);

        PrimaryKeyHelper.ValidatePrimaryKey(typeof(T), "pagination");
        var idColumnName = PrimaryKeyHelper.GetPrimaryKeyColumnName(typeof(T))!;

        var offset = (pageNumber - 1) * pageSize;
        var sql = $"SELECT {GetProjection()} FROM {FormatTableName(_tableName)} ORDER BY {idColumnName} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
        return await _context.QueryAsync<T>(sql);
    }

    public async Task<IEnumerable<T>> PageAsync(int pageNumber, int pageSize, Expression<Func<T, object>> orderBy, bool ascending = true)
    {
        ValidationHelper.ValidatePagination(pageNumber, pageSize);
        ValidationHelper.ValidateExpression(orderBy, nameof(orderBy));

        var orderByVisitor = CreateOrderByVisitor();
        var orderByClause = orderByVisitor.TranslateOrderBy(orderBy, ascending);
        
        var offset = (pageNumber - 1) * pageSize;
        var sql = $"SELECT {GetProjection()} FROM {FormatTableName(_tableName)} ORDER BY {orderByClause} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
        return await _context.QueryAsync<T>(sql);
    }

    public async Task<(IEnumerable<T> Data, int TotalCount)> PageWithCountAsync(int pageNumber, int pageSize)
    {
        ValidationHelper.ValidatePagination(pageNumber, pageSize);

        PrimaryKeyHelper.ValidatePrimaryKey(typeof(T), "pagination");
        var idColumnName = PrimaryKeyHelper.GetPrimaryKeyColumnName(typeof(T))!;

        var offset = (pageNumber - 1) * pageSize;
        var sql = $@"
            SELECT {GetProjection()} FROM {FormatTableName(_tableName)} ORDER BY {idColumnName} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY;
            SELECT COUNT(*) FROM {FormatTableName(_tableName)};";
        
        using var multi = await _context.Connection.QueryMultipleAsync(sql);
        var data = await multi.ReadAsync<T>();
        var totalCount = await multi.ReadSingleAsync<int>();
        
        return (data, totalCount);
    }

    public async Task<(IEnumerable<T> Data, int TotalCount)> PageWithCountAsync(int pageNumber, int pageSize, Expression<Func<T, object>> orderBy, bool ascending = true)
    {
        ValidationHelper.ValidatePagination(pageNumber, pageSize);
        ValidationHelper.ValidateExpression(orderBy, nameof(orderBy));

        var orderByVisitor = CreateOrderByVisitor();
        var orderByClause = orderByVisitor.TranslateOrderBy(orderBy, ascending);
        
        var offset = (pageNumber - 1) * pageSize;
        var sql = $@"
            SELECT {GetProjection()} FROM {FormatTableName(_tableName)} ORDER BY {orderByClause} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY;
            SELECT COUNT(*) FROM {FormatTableName(_tableName)};";
        
        using var multi = await _context.Connection.QueryMultipleAsync(sql);
        var data = await multi.ReadAsync<T>();
        var totalCount = await multi.ReadSingleAsync<int>();
        
        return (data, totalCount);
    }

    public async Task<IEnumerable<T>> IncludeAsync<TProperty>(Expression<Func<T, TProperty>> includeExpression)
    {
        var navigationInfo = NavigationPropertyAnalyzer.AnalyzeIncludeExpression(includeExpression);
        var visitor = CreateIncludeVisitor(typeof(T), navigationInfo);
        var (sql, parameters) = visitor.GenerateIncludeQuery<T>();
        
        var result = await _context.Connection.QueryAsync(sql, parameters);
        return MapIncludeResults<TProperty>(result, navigationInfo);
    }

    public async Task<IEnumerable<T>> IncludeAsync<TProperty>(Expression<Func<T, bool>> predicate, Expression<Func<T, TProperty>> includeExpression)
    {
        var navigationInfo = NavigationPropertyAnalyzer.AnalyzeIncludeExpression(includeExpression);
        var visitor = CreateIncludeVisitor(typeof(T), navigationInfo);
        var (sql, parameters) = visitor.GenerateIncludeQuery<T>(predicate);
        
        var result = await _context.Connection.QueryAsync(sql, parameters);
        return MapIncludeResults<TProperty>(result, navigationInfo);
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
        var visitor = CreateMultiLevelIncludeVisitor(typeof(T));
        var sql = visitor.GenerateMultiLevelIncludeQuery<T>(includes);
        var result = await _context.Connection.QueryAsync(sql.Sql, sql.Parameters);
        return MapMultiLevelResults(result, visitor.GetIncludeLevels());
    }

    private IEnumerable<T> MapMultiLevelResults(IEnumerable<dynamic> results, List<IncludeLevel> includeLevels)
    {
        var mapper = new MultiLevelResultMapper<T>(includeLevels);
        return mapper.MapResults(results);
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
                if (property.Key.StartsWith(GetSourcePrefix()))
                {
                    entityData[property.Key.Substring(2)] = property.Value;
                }
                else if (property.Key.StartsWith(GetTargetPrefix()))
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
        var primaryKeyProperty = PrimaryKeyHelper.GetPrimaryKeyProperty(typeof(T));
        if (primaryKeyProperty != null)
        {
            var columnName = GetEntityKeyColumnName(PrimaryKeyHelper.GetPrimaryKeyColumnName(typeof(T))!);
            return entityData.ContainsKey(columnName) ? entityData[columnName] : 0;
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
            var columnName = GetMappingColumnName(columnAttr?.Name ?? property.Name);
            
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
        
        var primaryKeyProperty = PrimaryKeyHelper.GetPrimaryKeyProperty(entity1.GetType());
        if (primaryKeyProperty != null)
        {
            var id1 = primaryKeyProperty.GetValue(entity1);
            var id2 = primaryKeyProperty.GetValue(entity2);
            return Equals(id1, id2);
        }
        
        return entity1.Equals(entity2);
    }

    private bool IsCollectionType(Type type)
    {
        return type.IsGenericType &&
               (typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition()) ||
                type.GetGenericTypeDefinition() == typeof(List<>) ||
                type.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                type.GetGenericTypeDefinition() == typeof(IList<>));
    }

    protected string GetProjection()
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

    protected abstract string FormatParameter(string parameterName);
    protected abstract string FormatTableName(string tableName);
    protected abstract string GenerateExistsQuery(string subQuery);
    protected abstract string GetSourcePrefix();
    protected abstract string GetTargetPrefix();
    protected abstract string GetEntityKeyColumnName(string columnName);
    protected abstract string GetMappingColumnName(string columnName);
    
    protected abstract BasePredicateVisitor CreatePredicateVisitor();
    protected abstract BaseOrderByVisitor CreateOrderByVisitor();
    protected abstract BaseProjectionVisitor CreateProjectionVisitor(Type entityType);
    protected abstract BaseIncludeVisitor CreateIncludeVisitor(Type entityType, NavigationPropertyInfo navigationInfo);
    protected abstract MultiLevelIncludeVisitor CreateMultiLevelIncludeVisitor(Type rootType);
}
