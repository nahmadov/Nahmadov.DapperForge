using System.Linq.Expressions;

using DapperToolkit.Core.Builders;
using DapperToolkit.Core.Mapping;
using DapperToolkit.Core.Validation;

namespace DapperToolkit.Core.Context;

public sealed class DapperSet<TEntity> where TEntity : class
{
    private readonly DapperDbContext _context;
    private readonly SqlGenerator<TEntity> _generator;
    private readonly EntityMapping _mapping;

    internal DapperSet(DapperDbContext context, SqlGenerator<TEntity> generator, EntityMapping mapping)
    {
        _context = context;
        _generator = generator;
        _mapping = mapping;
    }

    #region Query

    public Task<IEnumerable<TEntity>> GetAllAsync()
        => _context.QueryAsync<TEntity>(_generator.SelectAllSql);

    public Task<TEntity?> FindAsync(object key)
    {
        if (_mapping.KeyProperty is null)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has no key and does not support FindAsync.");
        }

        if (string.IsNullOrWhiteSpace(_generator.SelectByIdSql))
        {
            throw new InvalidOperationException(
                $"FindAsync is not configured for entity '{typeof(TEntity).Name}'. " +
                "Ensure the entity has a key and a proper mapping.");
        }

        var param = new Dictionary<string, object?>
        {
            [_mapping.KeyProperty.Name] = key
        };

        return _context.QueryFirstOrDefaultAsync<TEntity>(_generator.SelectByIdSql, param);
    }

    public Task<IEnumerable<TEntity>> WhereAsync(Expression<Func<TEntity, bool>> predicate)
    {
        var dialect = _generator.Dialect;
        var visitor = new PredicateVisitor<TEntity>(_mapping, dialect);
        var (sql, parameters) = visitor.Translate(predicate);

        var finalSql = $"{_generator.SelectAllSql} WHERE {sql}";
        return _context.QueryAsync<TEntity>(finalSql, parameters);
    }

    public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate)
    {
        var dialect = _generator.Dialect;
        var visitor = new PredicateVisitor<TEntity>(_mapping, dialect);
        var (sql, parameters) = visitor.Translate(predicate);

        var finalSql = $"{_generator.SelectAllSql} WHERE {sql}";
        return _context.QueryFirstOrDefaultAsync<TEntity>(finalSql, parameters);
    }

    #endregion

    #region Insert / Update / Delete

    public Task<int> InsertAsync(TEntity entity)
    {
        EnsureCanMutate();
        EntityValidator<TEntity>.ValidateForInsert(entity, _mapping);
        if (string.IsNullOrWhiteSpace(_generator.InsertSql))
        {
            throw new InvalidOperationException(
                $"Insert SQL is not configured for entity '{typeof(TEntity).Name}'.");
        }

        return _context.ExecuteAsync(_generator.InsertSql, entity);
    }

    public Task<int> UpdateAsync(TEntity entity)
    {
        EnsureCanMutate();
        EntityValidator<TEntity>.ValidateForUpdate(entity, _mapping);
        if (string.IsNullOrWhiteSpace(_generator.UpdateSql))
        {
            throw new InvalidOperationException(
                $"Update SQL is not configured for entity '{typeof(TEntity).Name}'.");
        }

        return _context.ExecuteAsync(_generator.UpdateSql, entity);
    }

    public Task<int> DeleteAsync(TEntity entity)
    {
        EnsureCanMutate();
        if (string.IsNullOrWhiteSpace(_generator.DeleteByIdSql))
        {
            throw new InvalidOperationException(
                $"Delete SQL is not configured for entity '{typeof(TEntity).Name}'.");
        }

        return _context.ExecuteAsync(_generator.DeleteByIdSql, entity);
    }

    public Task<int> DeleteByIdAsync(object key)
    {
        EnsureCanMutate();

        if (_mapping.KeyProperty is null)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has no key and cannot be deleted by id.");
        }

        if (string.IsNullOrWhiteSpace(_generator.DeleteByIdSql))
        {
            throw new InvalidOperationException(
                $"Delete SQL is not configured for entity '{typeof(TEntity).Name}'.");
        }

        var param = new Dictionary<string, object?>
        {
            [_mapping.KeyProperty.Name] = key
        };

        return _context.ExecuteAsync(_generator.DeleteByIdSql, param);
    }

    public async Task<TKey> InsertAndGetIdAsync<TKey>(TEntity entity)
    {
        EnsureCanMutate();
        EntityValidator<TEntity>.ValidateForInsert(entity, _mapping);

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
            if (keyProp is not null)
            {
                var targetType = keyProp.PropertyType;

                object? converted = id;
                if (!targetType.IsAssignableFrom(typeof(TKey)))
                {
                    converted = Convert.ChangeType(id, targetType);
                }

                keyProp.SetValue(entity, converted);
            }
        }
        catch
        {
            // burada fail etsə də kritik deyil: caller yenə id-ni alıb qaytaracaq
        }

        return id;
    }

    #endregion

    private void EnsureCanMutate()
    {
        if (_mapping.IsReadOnly)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked as ReadOnly and cannot be modified.");
        }

        if (_mapping.KeyProperty is null)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has no key and cannot be updated/deleted.");
        }
    }
}
