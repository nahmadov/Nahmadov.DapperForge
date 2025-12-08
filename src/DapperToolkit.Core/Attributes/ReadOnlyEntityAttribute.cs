namespace DapperToolkit.Core.Attributes;

/// <summary>
/// Marks an entity type as read-only so insert, update, and delete operations are skipped.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ReadOnlyEntityAttribute : Attribute { }
