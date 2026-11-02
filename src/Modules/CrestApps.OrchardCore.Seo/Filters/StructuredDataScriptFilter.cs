using CrestApps.OrchardCore.Seo.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;
using OrchardCore.Modules;
using OrchardCore.ResourceManagement;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Seo.Filters;

/// <summary>
/// Writes the JSON-LD script of the page in the head of every front-end page: the site's publisher and web site, and
/// the content item the page displays. It writes one script, so all the structured data of the page is in one graph.
/// </summary>
internal sealed class StructuredDataScriptFilter : IAsyncResultFilter
{
    private readonly IPageStructuredData _pageStructuredData;
    private readonly IStructuredDataManager _structuredDataManager;
    private readonly ISiteService _siteService;
    private readonly IResourceManager _resourceManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredDataScriptFilter"/> class.
    /// </summary>
    /// <param name="pageStructuredData">The structured data of the current page.</param>
    /// <param name="structuredDataManager">The structured data manager.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="resourceManager">The resource manager, which writes the script in the head.</param>
    /// <param name="logger">The logger.</param>
    public StructuredDataScriptFilter(
        IPageStructuredData pageStructuredData,
        IStructuredDataManager structuredDataManager,
        ISiteService siteService,
        IResourceManager resourceManager,
        ILogger<StructuredDataScriptFilter> logger)
    {
        _pageStructuredData = pageStructuredData;
        _structuredDataManager = structuredDataManager;
        _siteService = siteService;
        _resourceManager = resourceManager;
        _logger = logger;
    }

    /// <summary>
    /// Adds the JSON-LD script to the page before it renders.
    /// </summary>
    /// <param name="context">The result context.</param>
    /// <param name="next">The next step of the pipeline.</param>
    /// <returns>A task that completes when the result has run.</returns>
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.Result is (ViewResult or PageResult) &&
            !_pageStructuredData.Suppressed &&
            !AdminAttribute.IsApplied(context.HttpContext))
        {
            await RegisterScriptAsync();
        }

        await next();
    }

    private async Task RegisterScriptAsync()
    {
        var settings = await _siteService.GetSettingsAsync<StructuredDataSettings>();

        if (!settings.RenderOnPages)
        {
            return;
        }

        try
        {
            var document = await _structuredDataManager.BuildPageDocumentAsync(_pageStructuredData.ContentItem, _pageStructuredData.Nodes);

            if (document is null)
            {
                return;
            }

            _resourceManager.RegisterHeadScript(new HtmlString($"<script type=\"application/ld+json\">{JsonLd.ToScriptJson(document)}</script>"));
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            // The page renders without its structured data rather than failing.
            _logger.LogError(ex, "The JSON-LD of the page could not be built.");
        }
    }
}
