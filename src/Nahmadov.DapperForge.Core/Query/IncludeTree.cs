using System.Reflection;

namespace Nahmadov.DapperForge.Core.Query;

internal sealed class IncludeTree
{
    public List<IncludeNode> Roots { get; } = new();
}

internal sealed class IncludeNode
{
    public required PropertyInfo Navigation { get; init; }
    public required Type RelatedType { get; init; }
    public required bool IsCollection { get; init; }
    public List<IncludeNode> Children { get; } = new();
}