using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace Nahmadov.DapperForge.UnitTests.Fakes;
#nullable disable
public class FakeDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;
    private readonly ConcurrentQueue<IEnumerable<object>> _queryResults = new();
    private readonly ConcurrentQueue<int> _nonQueryResults = new();
    private readonly ConcurrentQueue<object> _scalarResults = new();
    private IEnumerable<object> _lastQuery = Enumerable.Empty<object>();
    private int _lastNonQuery;
    private object _lastScalar;

    public int OpenCount { get; private set; }
    public int DisposeCount { get; private set; }

    private Exception _openFailureException;

    public void SetState(ConnectionState state) => _state = state;

    /// <summary>
    /// Simulates a broken connection state.
    /// </summary>
    public void SimulateConnectionBreak()
    {
        _state = ConnectionState.Broken;
    }

    /// <summary>
    /// Simulates a failure during connection opening.
    /// </summary>
    /// <param name="exception">The exception to throw when Open() is called.</param>
    public void SimulateOpenFailure(Exception exception)
    {
        _openFailureException = exception;
    }

    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => "FakeDb";
    public override string DataSource => "FakeSource";
    public override string ServerVersion => "1.0";
    public override ConnectionState State => _state;

    public void SetupQuery<T>(IEnumerable<T> results)
    {
        var materialized = results.Cast<object>().ToArray();
        _queryResults.Enqueue(materialized);
        _lastQuery = materialized;
    }

    public void SetupSplitQuery<T>(IEnumerable<T> results) => SetupQuery(results);

    public void SetupExecute(int affectedRows)
    {
        _nonQueryResults.Enqueue(affectedRows);
        _lastNonQuery = affectedRows;
    }

    public void SetupScalar<T>(T value)
    {
        _scalarResults.Enqueue(value);
        _lastScalar = value;
    }

    internal IEnumerable<object> DequeueQuery()
    {
        if (_queryResults.TryDequeue(out var result))
            return result;

        if (_scalarResults.TryDequeue(out var scalar))
            return new object[] { scalar };

        return _lastQuery;
    }

    internal int DequeueNonQuery()
    {
        if (_nonQueryResults.TryDequeue(out var result))
            return result;
        return _lastNonQuery;
    }

    internal object DequeueScalar()
    {
        if (_scalarResults.TryDequeue(out var result))
            return result;
        return _lastScalar;
    }

    public override void ChangeDatabase(string databaseName) { }

    public override void Close()
    {
        _state = ConnectionState.Closed;
    }

    public override void Open()
    {
        if (_openFailureException != null)
        {
            var ex = _openFailureException;
            _openFailureException = null; // Reset after throwing
            throw ex;
        }

        _state = ConnectionState.Open;
        OpenCount++;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        => new FakeDbTransaction(this);

    protected override DbCommand CreateDbCommand()
        => new FakeDbCommand(this);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DisposeCount++;
        _state = ConnectionState.Closed;
    }
}
#nullable restore
