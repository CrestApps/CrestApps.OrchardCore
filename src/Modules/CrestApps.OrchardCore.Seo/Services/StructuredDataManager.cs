using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Seo.Models;
using CrestApps.OrchardCore.Seo.SchemaOrg;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Seo.Services;

/// <summary>
/// Builds the structured data of pages and content items by running every <see cref="IStructuredDataProvider"/>.
/// </summary>
internal sealed class StructuredDataManager : IStructuredDataManager
{
    private readonly IEnumerable<IStructuredDataProvider> _providers;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ContentItemUrlResolver _urlResolver;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly StructuredDataOptions _options;
    private readonly ILogger _logger;

    private IStructuredDataProvider[] _orderedProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredDataManager"/> class.
    /// </summary>
    /// <param name="providers">The structured data providers.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="urlResolver">The resolver of content item URLs.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="options">The structured data options.</param>
    /// <param name="logger">The logger.</param>
    public StructuredDataManager(
        IEnumerable<IStructuredDataProvider> providers,
        IContentDefinitionManager contentDefinitionManager,
        ContentItemUrlResolver urlResolver,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        IOptions<StructuredDataOptions> options,
        ILogger<StructuredDataManager> logger)
    {
        _providers = providers;
        _contentDefinitionManager = contentDefinitionManager;
        _urlResolver = urlResolver;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<JsonObject> BuildPageDocumentAsync(ContentItem contentItem, IEnumerable<JsonObject> additionalNodes = null, JsonObject pageEntity = null)
    {
        var context = await BuildContextAsync(contentItem, StructuredDataPurpose.Page, pageEntity);

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

        var context = await BuildContextAsync(contentItem, purpose, pageEntity: null);

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

    private async Task<StructuredDataContext> BuildContextAsync(ContentItem contentItem, StructuredDataPurpose purpose, JsonObject pageEntity)
    {
        var context = new StructuredDataContext(contentItem, purpose, await GetBaseUrlAsync());

        if (contentItem is not null)
        {
            var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);
            var schemaOrgPart = SchemaOrgPartDefinitions.Find(definition);

            if (IsDescribed(contentItem, definition, schemaOrgPart, purpose))
            {
                context.ConfiguredType = schemaOrgPart?.GetSettings<SchemaOrgPartSettings>().SchemaType?.Trim();
                context.Url = await _urlResolver.GetUrlAsync(contentItem, context);

                // The type is set before the providers run, so they can rely on it; a provider can still suggest a
                // more specific one, such as Product, when the Schema.org part chose none.
                context.MainEntity = new JsonObject
                {
                    [StructuredDataConstants.Keywords.Type] = GetDefaultType(context),
                };
            }
        }

        var mergePageEntity = context.MainEntity is not null;

        // A page that code describes, such as a hard-coded About page, gets its node before the providers run, so it
        // gets the page's URL, the publisher and the web site like a content item's node.
        if (pageEntity is not null && context.MainEntity is null)
        {
            context.MainEntity = (JsonObject)pageEntity.DeepClone();
            context.MainEntity.Remove(StructuredDataConstants.Keywords.Context);
            context.ConfiguredType = JsonLd.GetType(context.MainEntity);
            context.Url = context.ToAbsoluteUrl(JsonLd.GetString(context.MainEntity, "url")) ?? GetRequestUrl(context);

            JsonLd.SetIfNotEmpty(context.MainEntity, "url", context.Url);
            context.MainEntity[StructuredDataConstants.Keywords.Type] = GetDefaultType(context);
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

        // What code says about a page that shows a content item comes last, so it overrides the item's data.
        if (mergePageEntity && pageEntity is not null)
        {
            var properties = (JsonObject)pageEntity.DeepClone();
            properties.Remove(StructuredDataConstants.Keywords.Context);

            JsonLd.Merge(context.MainEntity, properties);
        }

        if (context.MainEntity is not null)
        {
            CompleteMainEntity(context);
        }

        return context;
    }

    private string GetRequestUrl(StructuredDataContext context)
    {
        var request = _httpContextAccessor.HttpContext?.Request;

        return request is null ? null : context.ToAbsoluteUrl($"{request.PathBase}{request.Path}");
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

    /// <summary>
    /// Decides whether the structured data describes a content item. A page describes the items whose content type has
    /// the Schema.org part, unless an editor turned it off, and the items of a part a module describes, such as a
    /// product. An answer describes every item, so an agent always gets at least its name and URL.
    /// </summary>
    /// <param name="contentItem">The content item.</param>
    /// <param name="definition">Its content type definition.</param>
    /// <param name="schemaOrgPart">The Schema.org part of the content type, if it has one.</param>
    /// <param name="purpose">What the structured data is built for.</param>
    /// <returns><see langword="true"/> when the structured data describes the content item.</returns>
    private bool IsDescribed(ContentItem contentItem, ContentTypeDefinition definition, ContentTypePartDefinition schemaOrgPart, StructuredDataPurpose purpose)
    {
        if (purpose != StructuredDataPurpose.Page)
        {
            return true;
        }

        if (schemaOrgPart is not null)
        {
            return !(contentItem.TryGet<SchemaOrgPart>(out var part) && part.Disabled);
        }

        return definition?.Parts.Any(part => _options.SelfDescribingParts.Contains(part.PartDefinition.Name)) == true;
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
