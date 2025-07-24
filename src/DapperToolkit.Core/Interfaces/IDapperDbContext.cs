using System.Data;

namespace DapperToolkit.Core.Interfaces;

public interface IDapperDbContext
{
    IDbConnection Connection { get; }
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null);
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null);
    Task<int> ExecuteAsync(string sql, object? param = null, IDbTransaction? transaction = null);
    Task<IDbTransaction> BeginTransactionAsync();
}