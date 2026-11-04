namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// The <see cref="IStructuredDataProvider.Order"/> values of the built-in providers, so other providers can run
/// before or after them.
/// </summary>
public static class StructuredDataProviderOrder
{
    /// <summary>
    /// The site-wide nodes and the content item defaults: name, URL, dates and language.
    /// </summary>
    public const int Defaults = -1000;

    /// <summary>
    /// The default order of a provider that describes a part or a domain type, such as a product.
    /// </summary>
    public const int Parts = 0;

    /// <summary>
    /// The SEO meta data an editor set on the content item, such as its description and social image.
    /// </summary>
    public const int SeoMeta = 500;

    /// <summary>
    /// The schema.org properties the Schema.org part's settings map, and the values editors enter on it.
    /// </summary>
    public const int SchemaOrgPart = 1000;

    /// <summary>
    /// The JSON-LD an editor typed on the content item. It runs last, so it overrides everything else.
    /// </summary>
    public const int EditorJsonLd = 2000;
}
