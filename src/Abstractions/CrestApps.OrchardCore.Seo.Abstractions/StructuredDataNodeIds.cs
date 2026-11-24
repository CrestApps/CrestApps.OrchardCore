namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// The identifiers of the site-wide nodes the Advanced SEO feature adds to a page's graph, so other providers can
/// refer to them.
/// </summary>
public static class StructuredDataNodeIds
{
    /// <summary>
    /// Gets the identifier of the site's publisher node.
    /// </summary>
    /// <param name="baseUrl">The absolute URL of the site.</param>
    /// <returns>The identifier.</returns>
    public static string Publisher(string baseUrl) => $"{baseUrl}/#publisher";

    /// <summary>
    /// Gets the identifier of the site's <c>WebSite</c> node.
    /// </summary>
    /// <param name="baseUrl">The absolute URL of the site.</param>
    /// <returns>The identifier.</returns>
    public static string WebSite(string baseUrl) => $"{baseUrl}/#website";
}
