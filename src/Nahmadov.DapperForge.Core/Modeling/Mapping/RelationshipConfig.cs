namespace Nahmadov.DapperForge.Core.Modeling.Mapping;
/// <summary>
/// Holds mutable configuration for a relationship between two entities,
/// collected via the fluent API.
/// </summary>
public class RelationshipConfig
{
    /// <summary>
    /// The navigation property name on the dependent entity (e.g., "Customer").
    /// </summary>
    public string NavigationPropertyName { get; set; } = string.Empty;

    /// <summary>
    /// The foreign key property name on the dependent entity (e.g., "CustomerId").
    /// </summary>
    public string? ForeignKeyPropertyName { get; set; }

    /// <summary>
    /// The CLR type of the principal entity (e.g., typeof(Customer)).
    /// </summary>
    public Type PrincipalEntityType { get; set; } = null!;

    /// <summary>
    /// The principal key property name. Defaults to the primary key if not specified.
    /// </summary>
    public string? PrincipalKeyPropertyName { get; set; }

    /// <summary>
    /// The inverse navigation property name on the principal entity (e.g., "Orders").
    /// Null when not specified via WithMany() or WithOne().
    /// </summary>
    public string? InverseNavigationPropertyName { get; set; }

    /// <summary>
    /// True if this is a reference navigation (HasOne), false for collection (HasMany).
    /// </summary>
    public bool IsReferenceNavigation { get; set; }

    /// <summary>
    /// The dependent entity type where the foreign key property resides.
    /// For HasOne, this is the entity being configured.
    /// For HasMany, this is the related entity type.
    /// </summary>
    public Type? DependentEntityType { get; set; }
}

