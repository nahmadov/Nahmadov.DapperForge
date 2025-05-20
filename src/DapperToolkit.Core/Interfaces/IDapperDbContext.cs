using System.Data;

namespace DapperToolkit.Core.Interfaces;

public interface IDapperDbContext
{
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null);
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null);
    Task<int> ExecuteAsync(string sql, object? param = null);
    Task<IDbTransaction> BeginTransactionAsync();
}