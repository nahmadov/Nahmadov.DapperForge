using System.Data;

using Dapper;

using DapperToolkit.Core.Common;
using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Core.Context;

public abstract class DapperDbContext : IDapperDbContext, IDisposable
{
    private readonly DapperDbContextOptions _options;
    private IDbConnection? _connection;
    private bool _disposed;

    protected DapperDbContext(DapperDbContextOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (_options.ConnectionFactory is null)
            throw new InvalidOperationException("ConnectionFactory is not configured.");
    }

    protected IDbConnection Connection
    {
        get
        {
            if (_connection != null)
            {
                if (_connection.State == ConnectionState.Open)
                    return _connection;

                if (_connection.State == ConnectionState.Broken)
                {
                    _connection.Dispose();
                    _connection = null;
                }
            }
            if (_options.ConnectionFactory is null)
                throw new InvalidOperationException("ConnectionFactory is not configured.");
            _connection ??= _options.ConnectionFactory();
            if (_connection.State != ConnectionState.Open)
                _connection.Open();

            return _connection;
        }
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? param = null,
        IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? Connection;
        return await connection.QueryAsync<T>(sql, param, transaction);
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql,
        object? param = null,
        IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? Connection;
        return await connection.QueryFirstOrDefaultAsync<T>(sql, param, transaction);
    }

    public async Task<int> ExecuteAsync(
        string sql,
        object? param = null,
        IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? Connection;
        return await connection.ExecuteAsync(sql, param, transaction);
    }

    public Task<IDbTransaction> BeginTransactionAsync()
    {
        var transaction = Connection.BeginTransaction();
        return Task.FromResult(transaction);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _connection?.Dispose();
            _connection = null;
        }

        _disposed = true;
    }
}