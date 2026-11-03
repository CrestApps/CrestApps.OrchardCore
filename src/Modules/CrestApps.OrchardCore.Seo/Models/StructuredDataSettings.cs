namespace CrestApps.OrchardCore.Seo.Models;

/// <summary>
/// The site settings of the structured data feature.
/// </summary>
public sealed class StructuredDataSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the site's pages get a JSON-LD script.
    /// </summary>
    public bool RenderOnPages { get; set; } = true;

    /// <summary>
    /// Gets or sets the schema.org type of the site's publisher: <c>Organization</c>, <c>Person</c>, or empty for
    /// none.
    /// </summary>
    public string PublisherType { get; set; } = StructuredDataPublisherTypes.Organization;

    /// <summary>
    /// Gets or sets the name of the publisher. The site name is used when it is empty.
    /// </summary>
    public string PublisherName { get; set; }

    /// <summary>
    /// Gets or sets the URL of the publisher's logo.
    /// </summary>
    public string PublisherLogoUrl { get; set; }

    /// <summary>
    /// Gets or sets the URLs of the publisher's profiles on other sites, such as social networks.
    /// </summary>
    public string[] SameAs { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the pages describe the site as a schema.org <c>WebSite</c>.
    /// </summary>
    public bool IncludeWebSite { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the <c>WebSite</c> tells search engines how to search the site, when
    /// Orchard Core's Search feature is enabled.
    /// </summary>
    public bool IncludeSearchAction { get; set; } = true;
}
