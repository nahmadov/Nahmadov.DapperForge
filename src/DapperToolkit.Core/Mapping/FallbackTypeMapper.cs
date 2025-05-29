using System.Reflection;

using Dapper;

namespace DapperToolkit.Core.Mapping;

public class FallbackTypeMapper(IEnumerable<SqlMapper.ITypeMap> mappers) : SqlMapper.ITypeMap
{
    private readonly IEnumerable<SqlMapper.ITypeMap> _mappers = mappers;

    public ConstructorInfo? FindConstructor(string[] names, Type[] types)
        => _mappers.Select(m => m.FindConstructor(names, types)).FirstOrDefault(c => c != null);

    public ConstructorInfo? FindExplicitConstructor()
        => _mappers.Select(m => m.FindExplicitConstructor()).FirstOrDefault(c => c != null);

    public SqlMapper.IMemberMap? GetConstructorParameter(ConstructorInfo constructor, string columnName)
        => _mappers.Select(m => m.GetConstructorParameter(constructor, columnName)).FirstOrDefault(p => p != null);

    public SqlMapper.IMemberMap? GetMember(string columnName)
        => _mappers.Select(m => m.GetMember(columnName)).FirstOrDefault(m => m != null);
}