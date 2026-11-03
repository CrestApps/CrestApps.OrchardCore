namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// Provides the constants of the Advanced SEO feature and its structured data (schema.org JSON-LD).
/// </summary>
public static class StructuredDataConstants
{
    /// <summary>
    /// The JSON-LD context of the schema.org vocabulary.
    /// </summary>
    public const string SchemaOrgContext = "https://schema.org";

    /// <summary>
    /// The settings group of the Advanced SEO site settings.
    /// </summary>
    public const string SettingsGroupId = "advanced-seo";

    /// <summary>
    /// The schema.org type used for a content item that has a URL but no other type.
    /// </summary>
    public const string DefaultPageType = "WebPage";

    /// <summary>
    /// The schema.org type used for a content item that has neither a URL nor another type.
    /// </summary>
    public const string DefaultThingType = "Thing";

    /// <summary>
    /// The feature identifiers.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The Advanced SEO feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Seo";
    }

    /// <summary>
    /// The JSON-LD keywords.
    /// </summary>
    public static class Keywords
    {
        /// <summary>
        /// The <c>@context</c> keyword.
        /// </summary>
        public const string Context = "@context";

        /// <summary>
        /// The <c>@type</c> keyword.
        /// </summary>
        public const string Type = "@type";

        /// <summary>
        /// The <c>@id</c> keyword.
        /// </summary>
        public const string Id = "@id";

        /// <summary>
        /// The <c>@graph</c> keyword.
        /// </summary>
        public const string Graph = "@graph";
    }
}
