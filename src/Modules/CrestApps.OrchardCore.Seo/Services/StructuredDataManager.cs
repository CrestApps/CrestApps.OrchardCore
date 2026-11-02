using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Seo.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Seo.Services;

/// <summary>
/// Builds the structured data of pages and content items by running every <see cref="IStructuredDataProvider"/>.
/// </summary>
internal sealed class StructuredDataManager : IStructuredDataManager
{
    private readonly IEnumerable<IStructuredDataProvider> _providers;
    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;
    private readonly ILogger _logger;

    private IStructuredDataProvider[] _orderedProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredDataManager"/> class.
    /// </summary>
    /// <param name="providers">The structured data providers.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="linkGenerator">The link generator, which builds the URL of a content item.</param>
    /// <param name="logger">The logger.</param>
    public StructuredDataManager(
        IEnumerable<IStructuredDataProvider> providers,
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        ILogger<StructuredDataManager> logger)
    {
        _providers = providers;
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<JsonObject> BuildPageDocumentAsync(ContentItem contentItem, IEnumerable<JsonObject> additionalNodes = null)
    {
        var context = await BuildContextAsync(contentItem, StructuredDataPurpose.Page);

        var graph = new JsonLdGraph();

        if (context.MainEntity is not null)
        {
            graph.Add(context.MainEntity);
        }

        foreach (var node in context.Graph.Nodes)
        {
            graph.Add(node);
        }

        if (additionalNodes is not null)
        {
            foreach (var node in additionalNodes)
            {
                graph.Add(node);
            }
        }

        return graph.ToDocument();
    }

    /// <inheritdoc/>
    public async Task<JsonObject> BuildEntityAsync(ContentItem contentItem, StructuredDataPurpose purpose)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        var context = await BuildContextAsync(contentItem, purpose);

        if (context.MainEntity is null)
        {
            return null;
        }

        var entity = new JsonObject
        {
            [StructuredDataConstants.Keywords.Context] = StructuredDataConstants.SchemaOrgContext,
        };

        JsonLd.Merge(entity, context.MainEntity);

        return entity;
    }

    private async Task<StructuredDataContext> BuildContextAsync(ContentItem contentItem, StructuredDataPurpose purpose)
    {
        var context = new StructuredDataContext(contentItem, purpose, await GetBaseUrlAsync());

        if (contentItem is not null)
        {
            var typeSettings = await GetTypeSettingsAsync(contentItem.ContentType);

            if (!typeSettings.Disabled)
            {
                context.ConfiguredType = typeSettings.SchemaType?.Trim();
                context.Url = await GetContentItemUrlAsync(contentItem, context);

                // The type is set before the providers run, so they can rely on it; a provider can still suggest a
                // more specific one, such as Product, when the content type settings chose none.
                context.MainEntity = new JsonObject
                {
                    [StructuredDataConstants.Keywords.Type] = GetDefaultType(context),
                };
            }
        }

        foreach (var provider in GetOrderedProviders())
        {
            try
            {
                await provider.BuildAsync(context);
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                // One broken provider must not take the page down; the rest of the data is still useful.
                _logger.LogError(ex, "The structured data provider '{Provider}' failed.", provider.GetType().FullName);
            }
        }

        if (context.MainEntity is not null)
        {
            CompleteMainEntity(context);
        }

        return context;
    }

    private static void CompleteMainEntity(StructuredDataContext context)
    {
        var entity = context.MainEntity;

        if (JsonLd.GetType(entity) is null)
        {
            entity[StructuredDataConstants.Keywords.Type] = GetDefaultType(context);
        }

        if (context.Url is not null && JsonLd.GetString(entity, StructuredDataConstants.Keywords.Id) is null)
        {
            entity[StructuredDataConstants.Keywords.Id] = $"{context.Url}#{JsonLd.GetType(entity).ToLowerInvariant()}";
        }
    }

    private static string GetDefaultType(StructuredDataContext context)
    {
        if (!string.IsNullOrEmpty(context.ConfiguredType))
        {
            return context.ConfiguredType;
        }

        return context.Url is null
            ? StructuredDataConstants.DefaultThingType
            : StructuredDataConstants.DefaultPageType;
    }

    private IStructuredDataProvider[] GetOrderedProviders()
        => _orderedProviders ??= _providers.OrderBy(provider => provider.Order).ToArray();

    private async Task<StructuredDataTypeSettings> GetTypeSettingsAsync(string contentType)
    {
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        return definition?.GetSettings<StructuredDataTypeSettings>() ?? new StructuredDataTypeSettings();
    }

    private async Task<string> GetContentItemUrlAsync(ContentItem contentItem, StructuredDataContext context)
    {
        var metadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(contentItem);

        if (metadata.DisplayRouteValues is null)
        {
            return null;
        }

        var httpContext = _httpContextAccessor.HttpContext;

        var path = httpContext is null
            ? _linkGenerator.GetPathByRouteValues(routeName: null, metadata.DisplayRouteValues)
            : _linkGenerator.GetPathByRouteValues(httpContext, routeName: null, metadata.DisplayRouteValues);

        return context.ToAbsoluteUrl(path);
    }

    private async Task<string> GetBaseUrlAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();

        if (!string.IsNullOrWhiteSpace(site.BaseUrl))
        {
            return site.BaseUrl.TrimEnd('/');
        }

        var request = _httpContextAccessor.HttpContext?.Request;

        if (request is null)
        {
            return string.Empty;
        }

        return $"{request.Scheme}://{request.Host}{request.PathBase}";
    }
}
