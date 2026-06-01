namespace Nahmadov.DapperForge.Core.Modeling.Schema;

/// <summary>
/// Infers a dialect-agnostic <see cref="SqlColumnType"/> and <see cref="ColumnTypeFacets"/> from a
/// CLR type. Used when a property has no explicit <c>HasColumnType(...)</c> configuration.
/// </summary>
public static class SqlColumnTypeInference
{
    /// <summary>
    /// Infers the logical column type and facets for the given CLR type.
    /// </summary>
    /// <param name="clrType">The CLR property type. <see cref="Nullable{T}"/> is unwrapped and sets <see cref="ColumnTypeFacets.IsNullable"/>.</param>
    /// <param name="maxLength">Optional configured maximum length (e.g. from <c>HasMaxLength</c>); feeds the <see cref="SqlColumnType.NVarChar"/> length.</param>
    /// <returns>The inferred column type and facets.</returns>
    /// <exception cref="NotSupportedException">The CLR type has no inference rule.</exception>
    public static (SqlColumnType Type, ColumnTypeFacets Facets) Infer(Type clrType, int? maxLength = null)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        var underlying = Nullable.GetUnderlyingType(clrType);
        var isNullable = underlying is not null || !clrType.IsValueType;
        var effective = underlying ?? clrType;

        if (effective.IsEnum)
            effective = Enum.GetUnderlyingType(effective);

        var (type, length, precision, scale) = Map(effective, maxLength);

        return (type, new ColumnTypeFacets(length, precision, scale, isNullable));
    }

    private static (SqlColumnType Type, int? Length, int? Precision, int? Scale) Map(Type type, int? maxLength)
    {
        if (type == typeof(bool)) return (SqlColumnType.Boolean, null, null, null);
        if (type == typeof(byte)) return (SqlColumnType.TinyInt, null, null, null);
        if (type == typeof(short)) return (SqlColumnType.SmallInt, null, null, null);
        if (type == typeof(int)) return (SqlColumnType.Int, null, null, null);
        if (type == typeof(long)) return (SqlColumnType.BigInt, null, null, null);

        if (type == typeof(decimal)) return (SqlColumnType.Decimal, null, 18, 2);
        if (type == typeof(double)) return (SqlColumnType.Float, null, null, null);
        if (type == typeof(float)) return (SqlColumnType.Real, null, null, null);

        if (type == typeof(DateTime)) return (SqlColumnType.DateTime2, null, null, null);
        if (type == typeof(DateOnly)) return (SqlColumnType.Date, null, null, null);
        if (type == typeof(TimeOnly)) return (SqlColumnType.Time, null, null, null);
        if (type == typeof(DateTimeOffset)) return (SqlColumnType.DateTimeOffset, null, null, null);

        if (type == typeof(Guid)) return (SqlColumnType.Guid, null, null, null);

        if (type == typeof(string))
            return (SqlColumnType.NVarChar, maxLength is > 0 ? maxLength : ColumnTypeFacets.Max, null, null);

        if (type == typeof(byte[])) return (SqlColumnType.VarBinary, ColumnTypeFacets.Max, null, null);

        throw new NotSupportedException(
            $"Cannot infer a SQL column type for CLR type '{type.FullName}'. " +
            "Configure it explicitly with HasColumnType(...).");
    }
}
