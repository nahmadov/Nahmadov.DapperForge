using System.Data;
using System.Text;

using Nahmadov.DapperForge.Core.Abstractions;

namespace Nahmadov.DapperForge.Oracle;

/// <summary>
/// Oracle-specific SQL dialect implementation.
/// </summary>
public class OracleDialect : ISqlDialect, IBulkSqlDialect
{
    public static readonly OracleDialect Instance = new();

    public string Name => "Oracle";

    public string? DefaultSchema => null;

    public string FormatParameter(string baseName) => ":" + baseName;

    public string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

    public string FormatTableAlias(string alias) => alias;

    /// <summary>
    /// Builds an INSERT statement that returns generated key values using RETURNING INTO.
    /// </summary>
    public string BuildInsertReturningId(string baseInsertSql, string tableName, params string[] keyColumnNames)
    {
        if (keyColumnNames is null || keyColumnNames.Length == 0)
            throw new ArgumentNullException(nameof(keyColumnNames));

        if (keyColumnNames.Length == 1)
        {
            var key = keyColumnNames[0];
            return $"{baseInsertSql} RETURNING {QuoteIdentifier(key)} INTO {FormatParameter(key)}";
        }

        var returning = string.Join(", ", keyColumnNames.Select(QuoteIdentifier));
        var into = string.Join(", ", keyColumnNames.Select(FormatParameter));
        return $"{baseInsertSql} RETURNING {returning} INTO {into}";
    }

    public string FormatBoolean(bool value) => value ? "1" : "0";

    public bool TryMapDbType(Type clrType, out DbType dbType)
    {
        clrType = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (clrType.IsEnum)
            clrType = Enum.GetUnderlyingType(clrType);

        if (clrType == typeof(int)) { dbType = DbType.Int32; return true; }
        if (clrType == typeof(long)) { dbType = DbType.Int64; return true; }
        if (clrType == typeof(short)) { dbType = DbType.Int16; return true; }
        if (clrType == typeof(byte)) { dbType = DbType.Byte; return true; }

        if (clrType == typeof(decimal)) { dbType = DbType.Decimal; return true; }
        if (clrType == typeof(double)) { dbType = DbType.Double; return true; }
        if (clrType == typeof(float)) { dbType = DbType.Single; return true; }

        if (clrType == typeof(bool)) { dbType = DbType.Int16; return true; }
        if (clrType == typeof(DateTime)) { dbType = DbType.DateTime; return true; }
        if (clrType == typeof(DateTimeOffset)) { dbType = DbType.DateTimeOffset; return true; }

        if (clrType == typeof(Guid)) { dbType = DbType.Guid; return true; }
        if (clrType == typeof(string)) { dbType = DbType.String; return true; }
        if (clrType == typeof(byte[])) { dbType = DbType.Binary; return true; }

        dbType = default;
        return false;
    }

    #region IBulkSqlDialect Implementation

    /// <inheritdoc />
    public int MaxParametersPerStatement => 32767;

    /// <inheritdoc />
    public string BuildBulkInsert(
        string tableName,
        IReadOnlyList<string> columns,
        int rowCount,
        Func<int, string, string> parameterNameGenerator)
    {
        if (rowCount == 0) return string.Empty;

        // Oracle INSERT ALL syntax
        var sb = new StringBuilder();
        sb.AppendLine("INSERT ALL");

        var columnList = string.Join(", ", columns.Select(QuoteIdentifier));

        for (int i = 0; i < rowCount; i++)
        {
            var values = columns.Select(c => FormatParameter(parameterNameGenerator(i, c)));
            sb.AppendLine($"  INTO {tableName} ({columnList}) VALUES ({string.Join(", ", values)})");
        }

        sb.Append("SELECT 1 FROM DUAL");
        return sb.ToString();
    }

    /// <inheritdoc />
    public string BuildMerge(
        string tableName,
        IReadOnlyList<string> keyColumns,
        IReadOnlyList<string> insertColumns,
        IReadOnlyList<string> updateColumns,
        int rowCount,
        Func<int, string, string> parameterNameGenerator,
        MergeMode mode)
    {
        if (rowCount == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"MERGE INTO {tableName} target");
        sb.AppendLine("USING (");

        // Build source with UNION ALL
        for (int i = 0; i < rowCount; i++)
        {
            if (i > 0) sb.AppendLine("  UNION ALL");
            sb.Append("  SELECT ");
            sb.Append(string.Join(", ", insertColumns.Select(c =>
                $"{FormatParameter(parameterNameGenerator(i, c))} AS {QuoteIdentifier(c)}")));
            sb.AppendLine(" FROM DUAL");
        }

        sb.AppendLine(") source");
        sb.Append("ON (");
        sb.Append(string.Join(" AND ",
            keyColumns.Select(k => $"target.{QuoteIdentifier(k)} = source.{QuoteIdentifier(k)}")));
        sb.AppendLine(")");

        if (mode != MergeMode.UpdateOnly)
        {
            sb.AppendLine("WHEN NOT MATCHED THEN");
            var cols = string.Join(", ", insertColumns.Select(QuoteIdentifier));
            var vals = string.Join(", ", insertColumns.Select(c => $"source.{QuoteIdentifier(c)}"));
            sb.AppendLine($"  INSERT ({cols}) VALUES ({vals})");
        }

        if (mode != MergeMode.InsertOnly && updateColumns.Count > 0)
        {
            sb.AppendLine("WHEN MATCHED THEN");
            sb.Append("  UPDATE SET ");
            sb.AppendLine(string.Join(", ",
                updateColumns.Select(c => $"target.{QuoteIdentifier(c)} = source.{QuoteIdentifier(c)}")));
        }

        return sb.ToString();
    }

    #endregion
}

