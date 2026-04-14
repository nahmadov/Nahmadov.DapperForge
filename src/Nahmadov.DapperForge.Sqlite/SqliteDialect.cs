using System.Data;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;
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
}