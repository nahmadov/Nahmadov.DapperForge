namespace Nahmadov.DapperForge.Core.Modeling.Schema;

/// <summary>
/// Dialect-agnostic logical SQL column type. Each dialect resolves a member of this enum
/// (together with <see cref="ColumnTypeFacets"/>) to a concrete DDL type name via
/// <see cref="Nahmadov.DapperForge.Core.Abstractions.ISqlDialect.GetColumnTypeSql"/>.
/// </summary>
public enum SqlColumnType
{
    /// <summary>Boolean value (SQL Server <c>bit</c>, SQLite <c>INTEGER</c>).</summary>
    Boolean,

    /// <summary>8-bit unsigned integer (SQL Server <c>tinyint</c>).</summary>
    TinyInt,

    /// <summary>16-bit integer (SQL Server <c>smallint</c>).</summary>
    SmallInt,

    /// <summary>32-bit integer (SQL Server <c>int</c>).</summary>
    Int,

    /// <summary>64-bit integer (SQL Server <c>bigint</c>).</summary>
    BigInt,

    /// <summary>Exact numeric with precision and scale (SQL Server <c>decimal(p,s)</c>).</summary>
    Decimal,

    /// <summary>Double-precision floating point (SQL Server <c>float</c>).</summary>
    Float,

    /// <summary>Single-precision floating point (SQL Server <c>real</c>).</summary>
    Real,

    /// <summary>Currency value (SQL Server <c>money</c>).</summary>
    Money,

    /// <summary>Date without time (SQL Server <c>date</c>).</summary>
    Date,

    /// <summary>Time without date (SQL Server <c>time</c>).</summary>
    Time,

    /// <summary>Legacy date and time (SQL Server <c>datetime</c>).</summary>
    DateTime,

    /// <summary>High-precision date and time (SQL Server <c>datetime2</c>).</summary>
    DateTime2,

    /// <summary>Date and time with time-zone offset (SQL Server <c>datetimeoffset</c>).</summary>
    DateTimeOffset,

    /// <summary>Fixed-length non-Unicode string (SQL Server <c>char(n)</c>).</summary>
    Char,

    /// <summary>Variable-length non-Unicode string (SQL Server <c>varchar(n)</c>).</summary>
    VarChar,

    /// <summary>Fixed-length Unicode string (SQL Server <c>nchar(n)</c>).</summary>
    NChar,

    /// <summary>Variable-length Unicode string (SQL Server <c>nvarchar(n)</c>).</summary>
    NVarChar,

    /// <summary>Large text (SQL Server <c>nvarchar(max)</c>, SQLite <c>TEXT</c>).</summary>
    Text,

    /// <summary>Fixed-length binary (SQL Server <c>binary(n)</c>).</summary>
    Binary,

    /// <summary>Variable-length binary (SQL Server <c>varbinary(n)</c> / <c>varbinary(max)</c>).</summary>
    VarBinary,

    /// <summary>Globally unique identifier (SQL Server <c>uniqueidentifier</c>, SQLite <c>TEXT</c>).</summary>
    Guid
}
