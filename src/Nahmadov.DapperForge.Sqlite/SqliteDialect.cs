using System.Data;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;
using Nahmadov.DapperForge.Core.Modeling.Schema;
using Nahmadov.DapperForge.Core.Querying.Predicates;
using Nahmadov.DapperForge.Sqlite.Date;

namespace Nahmadov.DapperForge.Sqlite;

/// <summary>
/// SQLite-specific dialect implementation.
/// </summary>
public class SqliteDialect : ISqlDialect
{
    public static readonly SqliteDialect Instance = new();

    public string Name => "Sqlite";

    public string? DefaultSchema => null;

    public string FormatParameter(string baseName) => "@" + baseName;

    public string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

    public string FormatTableAlias(string alias) => $"AS {alias}";

    /// <summary>
    /// Builds an INSERT statement that returns the generated rowid via last_insert_rowid().
    /// </summary>
    public string BuildInsertReturningId(string baseInsertSql, string tableName, params string[] keyColumnNames)
    {
        if (keyColumnNames is null || keyColumnNames.Length == 0)
            throw new ArgumentNullException(nameof(keyColumnNames));

        var key = keyColumnNames[0];
        return $"{baseInsertSql}; SELECT last_insert_rowid() AS {QuoteIdentifier(key)};";
    }

    public string FormatBoolean(bool value) => value ? "1" : "0";

    // ── Predicate translator factory ──────────────────────────────────────────

    /// <summary>
    /// Returns a <see cref="SqlitePredicateVisitor{TEntity}"/> that supports
    /// <see cref="SqliteDate.Date"/> and <see cref="SqliteDate.DateTime"/> expression markers
    /// in addition to all base predicate patterns.
    /// </summary>
    public PredicateVisitor<TEntity> CreatePredicateVisitor<TEntity>(EntityMapping mapping)
        where TEntity : class
        => new SqlitePredicateVisitor<TEntity>(mapping, this);

    /// <summary>
    /// Returns a <see cref="SqlitePredicateTranslator"/> so that Include-filter expressions
    /// also benefit from SQLite date-function support.
    /// </summary>
    public SqlPredicateTranslator CreatePredicateTranslator(EntityMapping mapping, string alias = "a", string paramPrefix = "p")
        => new SqlitePredicateTranslator(mapping, this, alias, paramPrefix);

    public bool TryMapDbType(Type clrType, out DbType dbType)
    {
        clrType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (clrType.IsEnum) clrType = Enum.GetUnderlyingType(clrType);

        if (clrType == typeof(int)) { dbType = DbType.Int32; return true; }
        if (clrType == typeof(long)) { dbType = DbType.Int64; return true; }
        if (clrType == typeof(short)) { dbType = DbType.Int16; return true; }
        if (clrType == typeof(byte)) { dbType = DbType.Byte; return true; }

        if (clrType == typeof(decimal)) { dbType = DbType.Decimal; return true; }
        if (clrType == typeof(double)) { dbType = DbType.Double; return true; }
        if (clrType == typeof(float)) { dbType = DbType.Single; return true; }

        if (clrType == typeof(bool)) { dbType = DbType.Boolean; return true; }
        if (clrType == typeof(DateTime)) { dbType = DbType.DateTime; return true; }
        if (clrType == typeof(DateTimeOffset)) { dbType = DbType.DateTimeOffset; return true; }

        if (clrType == typeof(Guid)) { dbType = DbType.Guid; return true; }
        if (clrType == typeof(string)) { dbType = DbType.String; return true; }
        if (clrType == typeof(byte[])) { dbType = DbType.Binary; return true; }

        dbType = default;
        return false;
    }

    /// <summary>
    /// Resolves a logical column type to a SQLite storage-class affinity name
    /// (<c>INTEGER</c>, <c>REAL</c>, <c>NUMERIC</c>, <c>TEXT</c>, <c>BLOB</c>).
    /// </summary>
    public string GetColumnTypeSql(SqlColumnType type, ColumnTypeFacets facets)
    {
        return type switch
        {
            SqlColumnType.Boolean
                or SqlColumnType.TinyInt
                or SqlColumnType.SmallInt
                or SqlColumnType.Int
                or SqlColumnType.BigInt => "INTEGER",

            SqlColumnType.Float
                or SqlColumnType.Real => "REAL",

            SqlColumnType.Decimal
                or SqlColumnType.Money => "NUMERIC",

            SqlColumnType.Date
                or SqlColumnType.Time
                or SqlColumnType.DateTime
                or SqlColumnType.DateTime2
                or SqlColumnType.DateTimeOffset
                or SqlColumnType.Char
                or SqlColumnType.VarChar
                or SqlColumnType.NChar
                or SqlColumnType.NVarChar
                or SqlColumnType.Text
                or SqlColumnType.Guid => "TEXT",

            SqlColumnType.Binary
                or SqlColumnType.VarBinary => "BLOB",

            _ => throw new NotSupportedException($"Unsupported SQL column type '{type}' for SQLite.")
        };
    }

    /// <inheritdoc />
    public bool SupportsSessionTempTables => true;

    /// <summary>
    /// Returns the quoted temp-table name. SQLite scopes temp tables via <c>CREATE TEMP TABLE</c>,
    /// so no name prefix is required.
    /// </summary>
    public string FormatTempTableName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Temp table name cannot be empty.", nameof(name));

        return QuoteIdentifier(name);
    }

    /// <summary>
    /// Builds a SQLite session temp table: <c>CREATE TEMP TABLE "Name" ( … )</c>.
    /// </summary>
    public string BuildCreateTempTable(string tempName, string columnsDdl)
        => $"CREATE TEMP TABLE {tempName} ({columnsDdl})";
}