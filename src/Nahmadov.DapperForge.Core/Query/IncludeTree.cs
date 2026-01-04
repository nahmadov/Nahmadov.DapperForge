using System.Reflection;

namespace Nahmadov.DapperForge.Core.Query;

/// <summary>
/// Represents a hierarchical structure of Include/ThenInclude operations.
/// </summary>
internal sealed class IncludeTree
{
    private readonly List<IncludeNode> _roots = [];

    public IReadOnlyList<IncludeNode> Roots => _roots;

    public bool HasIncludes => _roots.Count > 0;

    public IncludeNode AddRoot(PropertyInfo navigation, Type relatedType, bool isCollection)
    {
        var node = new IncludeNode(navigation, relatedType, isCollection);
        _roots.Add(node);
        return node;
    }

    public int GetTotalNodeCount()
    {
        var count = 0;
        foreach (var root in _roots)
        {
            count += 1 + root.GetDescendantCount();
        }
        return count;
    }
}

/// <summary>
/// Represents a single Include node in the tree.
/// </summary>
internal sealed class IncludeNode
{
    private readonly List<IncludeNode> _children = [];

    public IncludeNode(PropertyInfo navigation, Type relatedType, bool isCollection)
    {
        Navigation = navigation;
        RelatedType = relatedType;
        IsCollection = isCollection;
    }

    public PropertyInfo Navigation { get; }
    public Type RelatedType { get; }
    public bool IsCollection { get; }

    public IReadOnlyList<IncludeNode> Children => _children;
    public bool HasChildren => _children.Count > 0;

    public IncludeNode AddChild(PropertyInfo navigation, Type relatedType, bool isCollection)
    {
        var node = new IncludeNode(navigation, relatedType, isCollection);
        _children.Add(node);
        return node;
    }

    public int GetDescendantCount()
    {
        var count = 0;
        foreach (var child in _children)
        {
            count += 1 + child.GetDescendantCount();
        }
        return count;
    }
}
