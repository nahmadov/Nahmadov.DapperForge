using DapperToolkit.Core.Builders;

namespace DapperToolkit.Core.Context;

public sealed class DapperSet<TEntity> where TEntity : class
{
    private readonly DapperDbContext _context;
    private readonly SqlGenerator<TEntity> _generator;

    internal DapperSet(DapperDbContext context, SqlGenerator<TEntity> generator)
    {
        _context = context;
        _generator = generator;
    }

    public Task<IEnumerable<TEntity>> GetAllAsync()
        => _context.QueryAsync<TEntity>(_generator.SelectAllSql);

    public Task<TEntity?> FindAsync(object key)
    {
        var param = new Dictionary<string, object?>
        {
            [_generator.KeyPropertyName] = key
        };

        return _context.QueryFirstOrDefaultAsync<TEntity>(_generator.SelectByIdSql, param);
    }

    public Task<IEnumerable<TEntity>> QueryAsync(FilterExpression<TEntity> filter)
{
    var sql = $"{_generator.SelectAllSql} WHERE {filter.Sql}";
    return _context.QueryAsync<TEntity>(sql, filter.Parameters);
}

    public Task<TEntity?> FirstOrDefaultAsync(string whereClause, object? parameters = null)
    {
        var sql = $"{_generator.SelectAllSql} {whereClause}";
        return _context.QueryFirstOrDefaultAsync<TEntity>(sql, parameters);
    }

    public Task<int> InsertAsync(TEntity entity)
        => _context.ExecuteAsync(_generator.InsertSql, entity);

    public Task<int> UpdateAsync(TEntity entity)
        => _context.ExecuteAsync(_generator.UpdateSql, entity);

    public Task<int> DeleteAsync(TEntity entity)
        => _context.ExecuteAsync(_generator.DeleteByIdSql, entity);

    public Task<int> DeleteByIdAsync(object key)
    {
        var param = new Dictionary<string, object?>
        {
            [_generator.KeyPropertyName] = key
        };

        return _context.ExecuteAsync(_generator.DeleteByIdSql, param);
    }

    public async Task<TKey> InsertAndGetIdAsync<TKey>(TEntity entity)
    {
        if (_generator.InsertReturningIdSql is null)
        {
            throw new NotSupportedException(
                $"Dialect '{_generator.DialectName}' does not support InsertAndGetIdAsync. " +
                "Use InsertAsync and set the key manually or extend the dialect.");
        }

        var id = await _context.QueryFirstOrDefaultAsync<TKey>(_generator.InsertReturningIdSql, entity);

        if (id == null || EqualityComparer<TKey>.Default.Equals(id, default!))
        {
            throw new InvalidOperationException(
                $"Identity value returned NULL or default for entity '{typeof(TEntity).Name}'. " +
                "This likely means: (1) insert failed, (2) the provider cannot return identity, " +
                "(3) a trigger suppressed the identity value, or (4) SqlDialect needs adjustments.");
        }

        var keyProp = _generator.KeyProperty;
        try
        {
            var targetType = keyProp.PropertyType;

            object? converted = id;
            if (!targetType.IsAssignableFrom(typeof(TKey)))
            {
                converted = Convert.ChangeType(id, targetType);
            }

            keyProp.SetValue(entity, converted);
        }
        catch
        {
            // burda fail etsə də kritik deyil: caller yenə id-ni alıb qaytaracaq
        }

        return id;
    }
}