using System.Data;

using Dapper;

using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Exceptions;

namespace Nahmadov.DapperForge.Core.Context.Connection;

/// <summary>
/// Manages creation and lifecycle of the underlying database connection.
/// </summary>
internal sealed class ContextConnectionManager(
    DapperDbContextOptions options,
    Action<string> logInformation,
    Action<Exception, string?, string> logError) : IDisposable
{
    private readonly DapperDbContextOptions _options = options;
    private readonly object _connectionLock = new();
    private IDbConnection? _connection;
    private readonly Action<string> _logInformation = logInformation;
    private readonly Action<Exception, string?, string> _logError = logError;

    public IDbConnection GetOpenConnection()
    {
        lock (_connectionLock)
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
            {
                throw new DapperConfigurationException(
                    "ConnectionFactory is not configured. Provide a connection factory in the options.");
            }

            try
            {
                _connection ??= _options.ConnectionFactory();

                if (_connection is null)
                {
                    const string msg = "ConnectionFactory returned null";
                    _logError(new InvalidOperationException(msg), null, msg);
                    throw new DapperConnectionException(
                        "ConnectionFactory returned null. Ensure the factory creates a valid connection.");
                }

                if (_connection.State != ConnectionState.Open)
                {
                    _logInformation($"Opening database connection to {_connection.Database ?? "database"}");
                    _connection.Open();
                    _logInformation("Database connection opened successfully");
                }

                return _connection;
            }
            catch (DapperForgeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logError(ex, null, "Failed to establish database connection");
                throw new DapperConnectionException(
                    $"Failed to establish database connection: {ex.Message}", ex);
            }
        }
    }

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var connection = GetOpenConnection();

            if (connection.State != ConnectionState.Open)
            {
                _logInformation("Health check: Connection is not open");
                return false;
            }

            var sql = _options.Dialect!.Name.ToLowerInvariant() switch
            {
                "oracle" => "SELECT 1 FROM DUAL",
                _ => "SELECT 1"
            };

            await connection.QueryFirstOrDefaultAsync<int>(sql, commandTimeout: 5).ConfigureAwait(false);
            _logInformation("Health check: Connection is healthy");
            return true;
        }
        catch (Exception ex)
        {
            _logError(ex, null, "Health check failed");
            throw new DapperConnectionException($"Connection health check failed: {ex.Message}", ex);
        }
    }

    public Task EnsureConnectionHealthyAsync()
    {
        try
        {
            var connection = GetOpenConnection();

            if (connection.State == ConnectionState.Broken)
            {
                _logInformation("Connection is broken, attempting to reconnect");
                connection.Close();
                connection.Open();
            }
            else if (connection.State != ConnectionState.Open)
            {
                _logInformation("Connection is not open, opening connection");
                connection.Open();
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logError(ex, null, "Failed to ensure connection health");
            throw new DapperConnectionException($"Failed to ensure connection health: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
