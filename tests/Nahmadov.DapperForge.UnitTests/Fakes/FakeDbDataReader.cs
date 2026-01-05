using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Nahmadov.DapperForge.UnitTests.Fakes;
#nullable disable
internal sealed class FakeDbDataReader : DbDataReader
{
    private readonly List<object> _rows;
    private readonly PropertyInfo[] _properties = Array.Empty<PropertyInfo>();
    private readonly Dictionary<string, int> _ordinalLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _isScalar;
    private readonly Type _scalarType = typeof(object);
    private int _index = -1;

    public FakeDbDataReader(IEnumerable<object> data)
    {
        _rows = data?.ToList() ?? new List<object>();
        var first = _rows.FirstOrDefault();
        if (first is null)
            return;

        var type = first.GetType();
        _isScalar = type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type.IsEnum;
        if (_isScalar)
        {
            _scalarType = type;
            _ordinalLookup["Value"] = 0;
        }
        else
        {
            _properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (var i = 0; i < _properties.Length; i++)
            {
                _ordinalLookup[_properties[i].Name] = i;
            }
        }
    }

    private object Current => _rows[_index];

    public override int FieldCount => _isScalar ? 1 : _properties.Length;

    public override bool Read()
    {
        _index++;
        return _index < _rows.Count;
    }

    public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());

    public override bool NextResult() => false;

    public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => Task.FromResult(false);

    public override int Depth => 0;

    public override bool IsClosed => false;

    public override int RecordsAffected => 0;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override string GetName(int ordinal) => _isScalar ? "Value" : _properties[ordinal].Name;

    public override int GetOrdinal(string name) => _ordinalLookup.TryGetValue(name, out var ord) ? ord : -1;

    public override Type GetFieldType(int ordinal) => _isScalar ? _scalarType : _properties[ordinal].PropertyType;

    public override object GetValue(int ordinal)
    {
        if (_isScalar)
            return Current ?? DBNull.Value;

        var value = _properties[ordinal].GetValue(Current);
        return value ?? DBNull.Value;
    }

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }
        return count;
    }

    public override bool IsDBNull(int ordinal)
    {
        var value = GetValue(ordinal);
        return value is null || value is DBNull;
    }

    public override IEnumerator GetEnumerator() => _rows.GetEnumerator();

    public override bool HasRows => _rows.Count > 0;

    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        => throw new NotSupportedException();

    public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        => throw new NotSupportedException();

    public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));
    public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal));
    public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal));
    public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal));
    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));
    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));
    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));
    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
    public override string GetString(int ordinal) => Convert.ToString(GetValue(ordinal));
}
#nullable restore
