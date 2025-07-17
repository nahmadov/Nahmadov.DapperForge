using System.Data;

using Dapper;

using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Core.Context;

public class DapperDbContext(IDapperConnectionProvider provider) : IDapperDbContext
{
    private readonly IDapperConnectionProvider _provider = provider;
    public IDbConnection Connection => CreateConnection();

    private IDbConnection CreateConnection()
    {
        var conn = _provider.CreateConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();
        return conn;
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
        => await CreateConnection().QueryAsync<T>(sql, param);

    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
        => await CreateConnection().QueryFirstOrDefaultAsync<T>(sql, param);

    public async Task<int> ExecuteAsync(string sql, object? param = null)
        => await CreateConnection().ExecuteAsync(sql, param);

    public Task<IDbTransaction> BeginTransactionAsync()
    {
        throw new NotImplementedException();
    }
}