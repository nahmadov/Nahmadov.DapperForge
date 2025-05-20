using System.Data;

using Dapper;

using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Core.Context;

public class DapperDbContext(IDapperConnectionProvider provider) : IDapperDbContext
{
    private readonly IDapperConnectionProvider _provider = provider;

    private IDbConnection CreateConnection()
    {
        var conn = _provider.CreateConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        return conn;
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<T>(sql, param);
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<T>(sql, param);
    }

    public async Task<int> ExecuteAsync(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(sql, param);
    }

    public async Task<IDbTransaction> BeginTransactionAsync()
    {
        var conn = CreateConnection();
        return await Task.FromResult(conn.BeginTransaction());
    }
}