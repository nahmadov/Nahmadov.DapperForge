using System.Data;
using System.Globalization;
using System.Text;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Schema;

namespace Nahmadov.DapperForge.SqlServer;

/// <summary>
/// SQL Server-specific dialect implementation.
/// </summary>
public class SqlServerDialect : ISqlDialect, IBulkSqlDialect
{
    public static readonly SqlServerDialect Instance = new();

    public string Name => "SqlServer";

    public string? DefaultSchema => "dbo";

    public string FormatParameter(string baseName) => "@" + baseName;

    public string QuoteIdentifier(string identifier) => $"[{identifier}]";

    public string FormatTableAlias(string alias) => $"AS {alias}";

    /// <summary>
    /// Builds an INSERT statement that returns the generated identity value.
    /// </summary>
    public string BuildInsertReturningId(string baseInsertSql, string tableName, params string[] keyColumnNames)
    {
        if (keyColumnNames is null || keyColumnNames.Length == 0)
            throw new ArgumentNullException(nameof(keyColumnNames));

        var key = keyColumnNames[0];
        return $"{baseInsertSql}; SELECT CAST(SCOPE_IDENTITY() AS int) AS {QuoteIdentifier(key)};";
    }

    public string FormatBoolean(bool value) => value ? "1" : "0";

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
    /// Resolves a logical column type to its SQL Server DDL type name.
    /// </summary>
    public string GetColumnTypeSql(SqlColumnType type, ColumnTypeFacets facets)
    {
        return type switch
        {
            SqlColumnType.Boolean => "bit",
            SqlColumnType.TinyInt => "tinyint",
            SqlColumnType.SmallInt => "smallint",
            SqlColumnType.Int => "int",
            SqlColumnType.BigInt => "bigint",
            SqlColumnType.Decimal => $"decimal({Precision(facets, 18)},{Scale(facets, 2)})",
            SqlColumnType.Money => "money",
            SqlColumnType.Float => "float",
            SqlColumnType.Real => "real",
            SqlColumnType.Date => "date",
            SqlColumnType.Time => "time",
            SqlColumnType.DateTime => "datetime",
            SqlColumnType.DateTime2 => "datetime2",
            SqlColumnType.DateTimeOffset => "datetimeoffset",
            SqlColumnType.Char => $"char({Length(facets, 1)})",
            SqlColumnType.VarChar => $"varchar({LengthOrMax(facets)})",
            SqlColumnType.NChar => $"nchar({Length(facets, 1)})",
            SqlColumnType.NVarChar => $"nvarchar({LengthOrMax(facets)})",
            SqlColumnType.Text => "nvarchar(max)",
            SqlColumnType.Binary => $"binary({Length(facets, 1)})",
            SqlColumnType.VarBinary => $"varbinary({LengthOrMax(facets)})",
            SqlColumnType.Guid => "uniqueidentifier",
            _ => throw new NotSupportedException($"Unsupported SQL column type '{type}' for SQL Server.")
        };
    }

    private static string LengthOrMax(ColumnTypeFacets facets)
        => facets.IsMaxLength || facets.Length is null or 0
            ? "max"
            : facets.Length.Value.ToString(CultureInfo.InvariantCulture);

    private static string Length(ColumnTypeFacets facets, int fallback)
        => (facets.Length is > 0 ? facets.Length.Value : fallback).ToString(CultureInfo.InvariantCulture);

    private static string Precision(ColumnTypeFacets facets, int fallback)
        => (facets.Precision ?? fallback).ToString(CultureInfo.InvariantCulture);

    private static string Scale(ColumnTypeFacets facets, int fallback)
        => (facets.Scale ?? fallback).ToString(CultureInfo.InvariantCulture);

    #region IBulkSqlDialect Implementation

    /// <inheritdoc />
    public int MaxParametersPerStatement => 2100;

    /// <inheritdoc />
    public string BuildBulkInsert(
        string tableName,
        IReadOnlyList<string> columns,
        int rowCount,
        Func<int, string, string> parameterNameGenerator)
    {
        if (rowCount == 0) return string.Empty;

        var columnList = string.Join(", ", columns.Select(QuoteIdentifier));
        var valueRows = new List<string>(rowCount);

        for (int i = 0; i < rowCount; i++)
        {
            var values = columns.Select(c => FormatParameter(parameterNameGenerator(i, c)));
            valueRows.Add($"({string.Join(", ", values)})");
        }

        return $"INSERT INTO {tableName} ({columnList}) VALUES {string.Join(", ", valueRows)}";
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

        // Build source VALUES
        var sourceValues = BuildSourceValues(insertColumns, rowCount, parameterNameGenerator);
        var sourceColumns = string.Join(", ", insertColumns.Select(QuoteIdentifier));

        sb.AppendLine($"MERGE INTO {tableName} AS target");
        sb.AppendLine($"USING (VALUES {sourceValues}) AS source ({sourceColumns})");
        sb.Append("ON ");
        sb.AppendLine(string.Join(" AND ",
            keyColumns.Select(k => $"target.{QuoteIdentifier(k)} = source.{QuoteIdentifier(k)}")));

        if (mode != MergeMode.UpdateOnly)
        {
            sb.AppendLine("WHEN NOT MATCHED THEN");
            sb.AppendLine($"  INSERT ({sourceColumns})");
            sb.AppendLine($"  VALUES ({string.Join(", ", insertColumns.Select(c => $"source.{QuoteIdentifier(c)}"))})");
        }

        if (mode != MergeMode.InsertOnly && updateColumns.Count > 0)
        {
            sb.AppendLine("WHEN MATCHED THEN");
            sb.Append("  UPDATE SET ");
            sb.AppendLine(string.Join(", ",
                updateColumns.Select(c => $"target.{QuoteIdentifier(c)} = source.{QuoteIdentifier(c)}")));
        }

        sb.Append(';');
        return sb.ToString();
    }

    private string BuildSourceValues(
        IReadOnlyList<string> columns,
        int rowCount,
        Func<int, string, string> parameterNameGenerator)
    {
        var rows = new List<string>(rowCount);
        for (int i = 0; i < rowCount; i++)
        {
            var values = columns.Select(c => FormatParameter(parameterNameGenerator(i, c)));
            rows.Add($"({string.Join(", ", values)})");
        }
        return string.Join(", ", rows);
    }

    #endregion
}

