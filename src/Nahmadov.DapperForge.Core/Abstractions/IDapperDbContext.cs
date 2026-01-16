using System.Data;

namespace Nahmadov.DapperForge.Core.Abstractions;
/// <summary>
/// Defines the minimal database operations exposed by <see cref="Context.DapperDbContext"/>.
/// </summary>
internal interface IDapperDbContext
{
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null);

    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null);

    Task<int> ExecuteAsync(string sql, object? param = null, IDbTransaction? transaction = null);

    Task<IDbTransaction> BeginTransactionAsync();

    Task<IEnumerable<object>> QueryDynamicAsync(Type entityType, string sql, object? param = null, IDbTransaction? transaction = null);
}

