using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace Nahmadov.DapperForge.UnitTests.Fakes;
#nullable disable

/// <summary>
/// A minimal <see cref="DbConnection"/> that records every executed non-query command
/// (its SQL text and parameter count) so tests can assert batching behaviour without a real database.
/// </summary>
public sealed class RecordingDbConnection : DbConnection
{
    public List<RecordedCommand> ExecutedCommands { get; } = new();

    private ConnectionState _state = ConnectionState.Closed;

    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => "Recording";
    public override string DataSource => "Recording";
    public override string ServerVersion => "1.0";
    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName) { }
    public override void Close() => _state = ConnectionState.Closed;
    public override void Open() => _state = ConnectionState.Open;

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        => new FakeDbTransaction(this);

    protected override DbCommand CreateDbCommand() => new RecordingDbCommand(this);

    internal void Record(string sql, IEnumerable<string> parameterNames)
        => ExecutedCommands.Add(new RecordedCommand(sql, parameterNames.ToList()));

    public sealed record RecordedCommand(string Sql, IReadOnlyList<string> ParameterNames)
    {
        public int ParameterCount => ParameterNames.Count;

        /// <summary>Number of value tuples in the statement (one parameter per row ends in <c>_0</c>).</summary>
        public int RowCount => ParameterNames.Count(n => Regex.IsMatch(n, "_0$"));
    }

    private sealed class RecordingDbCommand(RecordingDbConnection conn) : DbCommand
    {
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; } = 30;
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection DbConnection { get => conn; set { } }
        protected override DbParameterCollection DbParameterCollection { get; } = new FakeDbParameterCollection();
        protected override DbTransaction DbTransaction { get; set; }

        public override void Cancel() { }
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new FakeDbParameter();

        private int Record()
        {
            var names = DbParameterCollection.Cast<DbParameter>().Select(p => p.ParameterName).ToList();
            conn.Record(CommandText, names);
            return names.Count(n => Regex.IsMatch(n, "_0$"));
        }

        public override int ExecuteNonQuery() => Record();
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromResult(Record());
        public override object ExecuteScalar() => null;
        public override Task<object> ExecuteScalarAsync(CancellationToken cancellationToken) => Task.FromResult<object>(null);

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            => new FakeDbDataReader(Array.Empty<object>());

        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
            => Task.FromResult(ExecuteDbDataReader(behavior));
    }
}
#nullable restore
