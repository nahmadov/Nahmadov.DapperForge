namespace Nahmadov.DapperForge.Core.Modeling.Schema;

/// <summary>
/// Optional sizing and nullability facets that refine a <see cref="SqlColumnType"/> when
/// resolving a concrete DDL type. Immutable; <see langword="default"/> represents "no facets".
/// </summary>
/// <param name="Length">
/// Length for string/binary types. Use <see cref="Max"/> (-1) to request the MAX/unbounded variant.
/// <see langword="null"/> means the dialect picks its default.
/// </param>
/// <param name="Precision">Total number of digits for <see cref="SqlColumnType.Decimal"/>/<see cref="SqlColumnType.Money"/>.</param>
/// <param name="Scale">Number of digits to the right of the decimal point.</param>
/// <param name="IsNullable">Whether the column accepts NULL.</param>
public readonly record struct ColumnTypeFacets(
    int? Length = null,
    int? Precision = null,
    int? Scale = null,
    bool IsNullable = false)
{
    /// <summary>Sentinel <see cref="Length"/> value requesting the MAX/unbounded variant.</summary>
    public const int Max = -1;

    /// <summary>True when <see cref="Length"/> requests the MAX/unbounded variant.</summary>
    public bool IsMaxLength => Length == Max;
}
