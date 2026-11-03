using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Seo.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Seo.Providers;

/// <summary>
/// Adds the site's publisher and the <c>WebSite</c> to every page, and links the page's content item to them. It runs
/// after every other provider so it only fills what is still missing.
/// </summary>
internal sealed class SiteStructuredDataProvider : IStructuredDataProvider
{
    /// <summary>
    /// The order of the provider: after the editor's own JSON-LD.
    /// </summary>
    internal const int ProviderOrder = StructuredDataProviderOrder.EditorJsonLd + 1000;

    private static readonly HashSet<string> _publishedWorkTypes = new(StringComparer.Ordinal)
    {
        "Article",
        "BlogPosting",
        "NewsArticle",
        "TechArticle",
        "Report",
        "ScholarlyArticle",
        "Blog",
        "WebPage",
        "AboutPage",
        "CollectionPage",
        "ContactPage",
        "FAQPage",
        "ItemPage",
        "ProfilePage",
        "SearchResultsPage",
    };

    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiteStructuredDataProvider"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public SiteStructuredDataProvider(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public int Order => ProviderOrder;

    /// <summary>
    /// Gets the identifier of the site's publisher node.
    /// </summary>
    /// <param name="baseUrl">The absolute URL of the site.</param>
    /// <returns>The identifier.</returns>
    internal static string GetPublisherId(string baseUrl) => $"{baseUrl}/#publisher";

    /// <summary>
    /// Gets the identifier of the site's <c>WebSite</c> node.
    /// </summary>
    /// <param name="baseUrl">The absolute URL of the site.</param>
    /// <returns>The identifier.</returns>
    internal static string GetWebSiteId(string baseUrl) => $"{baseUrl}/#website";

    /// <inheritdoc/>
    public async Task BuildAsync(StructuredDataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var site = await _siteService.GetSiteSettingsAsync();
        var settings = await _siteService.GetSettingsAsync<StructuredDataSettings>();

        string publisherId = null;
        string webSiteId = null;

        if (!string.IsNullOrEmpty(settings.PublisherType))
        {
            publisherId = GetPublisherId(context.BaseUrl);

            if (context.Purpose == StructuredDataPurpose.Page)
            {
                context.Graph.Add(BuildPublisher(context, settings, site.SiteName, publisherId));
            }
        }

        if (settings.IncludeWebSite)
        {
            webSiteId = GetWebSiteId(context.BaseUrl);

            if (context.Purpose == StructuredDataPurpose.Page)
            {
                var webSite = JsonLd.CreateNode("WebSite", webSiteId);
                webSite["url"] = $"{context.BaseUrl}/";
                JsonLd.SetIfNotEmpty(webSite, "name", site.SiteName);

                if (publisherId is not null)
                {
                    webSite["publisher"] = JsonLd.Reference(publisherId);
                }

                context.Graph.Add(webSite);
            }
        }

        // An agent's answer stands alone, so it gets no references to nodes it does not carry.
        if (context.Purpose != StructuredDataPurpose.Page || context.MainEntity is null)
        {
            return;
        }

        var type = JsonLd.GetType(context.MainEntity);

        if (type is null || !_publishedWorkTypes.Contains(type))
        {
            return;
        }

        if (publisherId is not null)
        {
            JsonLd.SetIfMissing(context.MainEntity, "publisher", JsonLd.Reference(publisherId));
        }

        if (webSiteId is not null)
        {
            JsonLd.SetIfMissing(context.MainEntity, "isPartOf", JsonLd.Reference(webSiteId));
        }
    }

    private static JsonObject BuildPublisher(StructuredDataContext context, StructuredDataSettings settings, string siteName, string publisherId)
    {
        var publisher = JsonLd.CreateNode(settings.PublisherType, publisherId);

        JsonLd.SetIfNotEmpty(publisher, "name", string.IsNullOrWhiteSpace(settings.PublisherName) ? siteName : settings.PublisherName);
        publisher["url"] = $"{context.BaseUrl}/";

        var logoUrl = context.ToAbsoluteUrl(settings.PublisherLogoUrl);

        if (!string.IsNullOrEmpty(logoUrl))
        {
            var logoProperty = settings.PublisherType == StructuredDataPublisherTypes.Person ? "image" : "logo";

            publisher[logoProperty] = new JsonObject
            {
                [StructuredDataConstants.Keywords.Type] = "ImageObject",
                ["url"] = logoUrl,
            };
        }

        var sameAs = new JsonArray();

        foreach (var profile in settings.SameAs ?? [])
        {
            if (!string.IsNullOrWhiteSpace(profile))
            {
                sameAs.Add(profile.Trim());
            }
        }

        if (sameAs.Count > 0)
        {
            publisher["sameAs"] = sameAs;
        }

        return publisher;
    }
}
