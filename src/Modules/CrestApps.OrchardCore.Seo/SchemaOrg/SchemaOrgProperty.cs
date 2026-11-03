namespace CrestApps.OrchardCore.Seo.SchemaOrg;

/// <summary>
/// A schema.org property the editor offers for a type.
/// </summary>
public sealed class SchemaOrgProperty
{
    /// <summary>
    /// Gets the property name. A dotted name, such as <c>offers.price</c>, is a property of a nested object.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the kind of value the property holds.
    /// </summary>
    public SchemaOrgValueKind Kind { get; init; }

    /// <summary>
    /// Gets the schema.org type of the object, when <see cref="Kind"/> is <see cref="SchemaOrgValueKind.Object"/>,
    /// such as <c>Person</c>.
    /// </summary>
    public string ObjectType { get; init; }

    /// <summary>
    /// Gets how much search engines need the property.
    /// </summary>
    public SchemaOrgPropertyLevel Level { get; init; }

    /// <summary>
    /// Gets a short description of the property.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets the value sources the editor tries first, such as <c>Item:DisplayText</c>.
    /// </summary>
    public string[] Suggestions { get; init; } = [];

    /// <summary>
    /// Gets a Liquid example the editor offers for a value too complex to map from one field, such as the questions of
    /// an FAQ page.
    /// </summary>
    public string LiquidExample { get; init; }

    /// <summary>
    /// Gets the values an <see cref="SchemaOrgValueKind.Enumeration"/> accepts.
    /// </summary>
    public SchemaOrgOption[] Options { get; init; } = [];
}

/// <summary>
/// One value of a schema.org enumeration.
/// </summary>
/// <param name="Value">The value written to the JSON-LD, such as <c>https://schema.org/InStock</c>.</param>
/// <param name="Text">The text shown to editors.</param>
public sealed record SchemaOrgOption(string Value, string Text);

/// <summary>
/// How much search engines need a property.
/// </summary>
public enum SchemaOrgPropertyLevel
{
    /// <summary>
    /// Useful, but neither required nor recommended.
    /// </summary>
    Optional,

    /// <summary>
    /// Recommended by Google for rich results.
    /// </summary>
    Recommended,

    /// <summary>
    /// Required by Google for rich results.
    /// </summary>
    Required,
}
