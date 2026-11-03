using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Seo.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Seo.Providers;

/// <summary>
/// Tells search engines how to search the site: a <c>SearchAction</c> on the <c>WebSite</c> node that points to
/// Orchard Core's search page. It is registered only when Orchard Core's Search feature is enabled.
/// </summary>
internal sealed class SearchActionStructuredDataProvider : IStructuredDataProvider
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchActionStructuredDataProvider"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public SearchActionStructuredDataProvider(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public int Order => SiteStructuredDataProvider.ProviderOrder + 1;

    /// <inheritdoc/>
    public async Task BuildAsync(StructuredDataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Purpose != StructuredDataPurpose.Page)
        {
            return;
        }

        var settings = await _siteService.GetSettingsAsync<StructuredDataSettings>();

        if (!settings.IncludeSearchAction)
        {
            return;
        }

        var webSite = context.Graph.Find(SiteStructuredDataProvider.GetWebSiteId(context.BaseUrl));

        if (webSite is null)
        {
            return;
        }

        webSite["potentialAction"] = new JsonObject
        {
            [StructuredDataConstants.Keywords.Type] = "SearchAction",
            ["target"] = new JsonObject
            {
                [StructuredDataConstants.Keywords.Type] = "EntryPoint",
                ["urlTemplate"] = $"{context.BaseUrl}/search?terms={{search_term_string}}",
            },
            ["query-input"] = "required name=search_term_string",
        };
    }
}
