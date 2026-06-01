using System.Data;

using Dapper;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Context.Connection;
using Nahmadov.DapperForge.Core.Modeling.Schema;

namespace Nahmadov.DapperForge.Core.Schema;

/// <summary>
/// Default <see cref="ITempTableBuilder"/> implementation. Generates dialect-specific
/// <c>CREATE [TEMP] TABLE</c> DDL from the configured columns and executes it via Dapper.
/// </summary>
internal sealed class TempTableBuilder : ITempTableBuilder
{
    private readonly ISqlDialect _dialect;
    private readonly string _name;
    private readonly List<ColumnDefinition> _columns = [];

    public TempTableBuilder(ISqlDialect dialect, string name)
    {
        _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Temp table name cannot be empty.", nameof(name));

        if (!_dialect.SupportsSessionTempTables)
            throw new NotSupportedException(
                $"Dialect '{_dialect.Name}' does not support session temp tables.");

        _name = name;
    }

    public ITempTableBuilder Column<T>(string name, bool nullable = false)
    {
        var (type, facets) = SqlColumnTypeInference.Infer(typeof(T));
        // Explicit nullable wins; a nullable CLR type also implies nullable.
        return Column(name, type, facets with { IsNullable = nullable || facets.IsNullable });
    }

    public ITempTableBuilder Column(string name, SqlColumnType type, ColumnTypeFacets facets = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Column name cannot be empty.", nameof(name));

        _columns.Add(new ColumnDefinition(name, type, facets));
        return this;
    }

    public string BuildCreateTableSql()
    {
        if (_columns.Count == 0)
            throw new InvalidOperationException(
                $"Temp table '{_name}' must declare at least one column before it can be created.");

        var tempName = _dialect.FormatTempTableName(_name);
        var columnsDdl = string.Join(", ", _columns.Select(FormatColumn));
        return _dialect.BuildCreateTempTable(tempName, columnsDdl);
    }

    public async Task CreateAsync(IDbConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var sql = BuildCreateTableSql();
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
    }

    public Task CreateAsync(IConnectionScope scope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return CreateAsync(scope.Connection, ct);
    }

    private string FormatColumn(ColumnDefinition column)
    {
        var quoted = _dialect.QuoteIdentifier(column.Name);
        var typeSql = _dialect.GetColumnTypeSql(column.Type, column.Facets);
        var nullability = column.Facets.IsNullable ? "NULL" : "NOT NULL";
        return $"{quoted} {typeSql} {nullability}";
    }

    private readonly record struct ColumnDefinition(string Name, SqlColumnType Type, ColumnTypeFacets Facets);
}
