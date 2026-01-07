using System.Data;

using Nahmadov.DapperForge.Core.Context.Connection;

namespace Nahmadov.DapperForge.UnitTests.Fakes;

/// <summary>
/// Fake connection scope for testing purposes.
/// </summary>
internal class FakeConnectionScope : IConnectionScope
{
    private readonly IDbConnection _connection;
    private bool _disposed;

    public FakeConnectionScope(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public IDbConnection Connection => _disposed ? throw new ObjectDisposedException(nameof(FakeConnectionScope)) : _connection;

    public bool HasActiveTransaction => false;

    public IDbTransaction? ActiveTransaction => null;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Close connection if open
        if (_connection.State == ConnectionState.Open)
        {
            _connection.Close();
        }

        _connection.Dispose();
    }
}
