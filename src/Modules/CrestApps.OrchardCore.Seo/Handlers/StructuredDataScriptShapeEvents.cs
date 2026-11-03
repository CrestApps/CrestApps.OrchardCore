using CrestApps.OrchardCore.Seo.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Implementation;
using OrchardCore.Modules;
using OrchardCore.ResourceManagement;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Seo.Handlers;

/// <summary>
/// Writes the JSON-LD script of the page in the head of every front-end page: the site's publisher and web site, the
/// content item the page displays, and what code said about the page. It writes one script, so all the structured
/// data of the page is in one graph.
/// </summary>
/// <remarks>
/// The script is built when the theme's <c>Layout</c> shape is displayed: after the page's own view has rendered, so a
/// Razor view or a Liquid template can still describe the page, and before the layout writes the head scripts.
/// </remarks>
internal sealed class StructuredDataScriptShapeEvents : IShapeDisplayEvents
{
    private readonly IPageStructuredData _pageStructuredData;
    private readonly IStructuredDataManager _structuredDataManager;
    private readonly ISiteService _siteService;
    private readonly IResourceManager _resourceManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;

    private bool _registered;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredDataScriptShapeEvents"/> class.
    /// </summary>
    /// <param name="pageStructuredData">The structured data of the current page.</param>
    /// <param name="structuredDataManager">The structured data manager.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="resourceManager">The resource manager, which writes the script in the head.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="logger">The logger.</param>
    public StructuredDataScriptShapeEvents(
        IPageStructuredData pageStructuredData,
        IStructuredDataManager structuredDataManager,
        ISiteService siteService,
        IResourceManager resourceManager,
        IHttpContextAccessor httpContextAccessor,
        ILogger<StructuredDataScriptShapeEvents> logger)
    {
        _pageStructuredData = pageStructuredData;
        _structuredDataManager = structuredDataManager;
        _siteService = siteService;
        _resourceManager = resourceManager;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task DisplayingAsync(ShapeDisplayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_registered ||
            context.Shape.Metadata.Type != "Layout" ||
            _pageStructuredData.Suppressed ||
            _httpContextAccessor.HttpContext is not { } httpContext ||
            AdminAttribute.IsApplied(httpContext))
        {
            return;
        }

        _registered = true;

        var settings = await _siteService.GetSettingsAsync<StructuredDataSettings>();

        if (!settings.RenderOnPages)
        {
            return;
        }

        try
        {
            var document = await _structuredDataManager.BuildPageDocumentAsync(
                _pageStructuredData.ContentItem,
                _pageStructuredData.Nodes,
                _pageStructuredData.MainEntity);

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

    /// <inheritdoc/>
    Task IShapeDisplayEvents.DisplayingFinalizedAsync(ShapeDisplayContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    Task IShapeDisplayEvents.DisplayedAsync(ShapeDisplayContext context)
        => Task.CompletedTask;
}
