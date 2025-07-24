using System.Data;

using Dapper;

using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Core.Context;

public class DapperDbContext(IDapperConnectionProvider provider) : IDapperDbContext, IDisposable
{
    private readonly IDapperConnectionProvider _provider = provider;
    private IDbConnection? _sharedConnection;
    public IDbConnection Connection => _sharedConnection ?? CreateConnection();

    private IDbConnection CreateConnection()
    {
        var conn = _provider.CreateConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();
        return conn;
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? Connection;
        return await connection.QueryAsync<T>(sql, param, transaction);
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? Connection;
        return await connection.QueryFirstOrDefaultAsync<T>(sql, param, transaction);
    }

    public async Task<int> ExecuteAsync(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? Connection;
        return await connection.ExecuteAsync(sql, param, transaction);
    }

    public Task<IDbTransaction> BeginTransactionAsync()
    {
        _sharedConnection = CreateConnection();
        var transaction = _sharedConnection.BeginTransaction();
        return Task.FromResult(transaction);
    }

    public void Dispose()
    {
        _sharedConnection?.Dispose();
        _sharedConnection = null;
    }
}