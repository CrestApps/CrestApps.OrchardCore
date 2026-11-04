namespace CrestApps.OrchardCore.Seo.SchemaOrg;

/// <summary>
/// A schema.org type the editor offers, with the properties it adds to its parent's.
/// </summary>
public sealed class SchemaOrgType
{
    /// <summary>
    /// Gets the type name, such as <c>BlogPosting</c>.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the name of the parent type, such as <c>Article</c>, whose properties this type also has.
    /// </summary>
    public string Parent { get; init; }

    /// <summary>
    /// Gets the group the type is listed under, or <see langword="null"/> for a base type that is not listed.
    /// </summary>
    public string Group { get; init; }

    /// <summary>
    /// Gets a short description of the type.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets the address of the search engine's guide to the type's rich results, if it has one.
    /// </summary>
    public string GuideUrl { get; init; }

    /// <summary>
    /// Gets the properties this type adds to its parent's, or makes more important.
    /// </summary>
    public SchemaOrgProperty[] Properties { get; init; } = [];
}
