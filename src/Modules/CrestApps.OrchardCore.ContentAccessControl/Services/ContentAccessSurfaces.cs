using System.Xml.Linq;
using CrestApps.OrchardCore.ContentAccessControl.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;
using OrchardCore.Feeds;
using OrchardCore.Feeds.Models;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Search.Abstractions;
using OrchardCore.Sitemaps.Builders;

namespace CrestApps.OrchardCore.ContentAccessControl.Sitemaps
{
    /// <summary>
    /// Leaves restricted content items out of every sitemap. A sitemap is built once and cached for every visitor, so a
    /// restricted item is left out whoever asks.
    /// </summary>
    public sealed class RestrictedContentSitemapFilter : ISitemapContentItemExtendedMetadataProvider
    {
        private readonly IContentDefinitionManager _contentDefinitionManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestrictedContentSitemapFilter"/> class.
        /// </summary>
        /// <param name="contentDefinitionManager">The content definition manager.</param>
        public RestrictedContentSitemapFilter(IContentDefinitionManager contentDefinitionManager)
        {
            _contentDefinitionManager = contentDefinitionManager;
        }

        /// <inheritdoc/>
        public XAttribute GetExtendedAttribute => null;

        /// <inheritdoc/>
        public async Task<bool> ApplyExtendedMetadataAsync(SitemapBuilderContext context, ContentItemsQueryContext queryContext, ContentItem contentItem, XElement url)
            => (await ContentAccessRestrictions.GetRoleNamesAsync(contentItem, _contentDefinitionManager)).Count == 0;
    }
}

namespace CrestApps.OrchardCore.ContentAccessControl.Feeds
{
    /// <summary>
    /// Wraps the feed item builder and takes out of the feed every item the reader may not view, so a restricted item's
    /// title and summary don't reach an RSS reader outside its roles.
    /// </summary>
    public sealed class RestrictedContentFeedItemBuilder : IFeedItemBuilder
    {
        private readonly IFeedItemBuilder _inner;
        private readonly IAuthorizationService _authorizationService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestrictedContentFeedItemBuilder"/> class.
        /// </summary>
        /// <param name="inner">The feed item builder it wraps.</param>
        /// <param name="authorizationService">The authorization service.</param>
        /// <param name="httpContextAccessor">The HTTP context accessor, for the reader.</param>
        public RestrictedContentFeedItemBuilder(IFeedItemBuilder inner, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
        {
            _inner = inner;
            _authorizationService = authorizationService;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        public async Task PopulateAsync(FeedContext context)
        {
            await _inner.PopulateAsync(context);

            if (context?.Response?.Items is not { Count: > 0 } items)
            {
                return;
            }

            var user = _httpContextAccessor.HttpContext?.User ?? new System.Security.Claims.ClaimsPrincipal();

            foreach (var item in items.OfType<FeedItem<ContentItem>>().ToList())
            {
                if (item.Item is null || await _authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, item.Item))
                {
                    continue;
                }

                // The feed builder already wrote the item into the channel.
                item.Element?.Remove();
                items.Remove(item);
            }
        }
    }
}

namespace CrestApps.OrchardCore.ContentAccessControl.Search
{
    /// <summary>
    /// Wraps a search service and takes out of its results every content item the searcher may not view, so the search
    /// page never shows a restricted item's title or summary outside its roles. The total count is lowered by the items
    /// taken out of the page; it can still count restricted items on other pages.
    /// </summary>
    public sealed class RestrictedContentSearchService : ISearchService
    {
        private readonly ISearchService _inner;
        private readonly IContentManager _contentManager;
        private readonly IAuthorizationService _authorizationService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestrictedContentSearchService"/> class.
        /// </summary>
        /// <param name="inner">The search service it wraps.</param>
        /// <param name="contentManager">The content manager.</param>
        /// <param name="authorizationService">The authorization service.</param>
        /// <param name="httpContextAccessor">The HTTP context accessor, for the searcher.</param>
        public RestrictedContentSearchService(ISearchService inner, IContentManager contentManager, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
        {
            _inner = inner;
            _contentManager = contentManager;
            _authorizationService = authorizationService;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        public string Name => _inner.Name;

        /// <inheritdoc/>
        public async Task<SearchResult> SearchAsync(IndexProfile index, string term, int start, int size)
        {
            var result = await _inner.SearchAsync(index, term, start, size);

            if (result?.ContentItemIds is not { Count: > 0 } ids)
            {
                return result;
            }

            var user = _httpContextAccessor.HttpContext?.User ?? new System.Security.Claims.ClaimsPrincipal();
            var items = (await _contentManager.GetAsync(ids, result.Latest ? VersionOptions.Latest : VersionOptions.Published))
                .Where(item => item is not null)
                .ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);
            var allowed = new List<string>(ids.Count);

            foreach (var id in ids)
            {
                // An id with no item is left to the search page, which shows nothing for it.
                if (!items.TryGetValue(id, out var item) || await _authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, item))
                {
                    allowed.Add(id);
                }
            }

            result.TotalCount = Math.Max(0, result.TotalCount - (ids.Count - allowed.Count));
            result.ContentItemIds = allowed;

            return result;
        }
    }
}

namespace CrestApps.OrchardCore.ContentAccessControl.Indexing
{
    /// <summary>
    /// Adds the roles a restricted content item is limited to to its search document, as the keyword field
    /// <see cref="RoleNamesField"/>, so a query or an index consumer (an AI data source, a custom search page) can filter
    /// by the reader's roles. Search engines have no hook to leave an item out of the index; the search page itself is
    /// filtered by <c>RestrictedContentSearchService</c>.
    /// </summary>
    public sealed class ContentAccessIndexHandler : IDocumentIndexHandler
    {
        /// <summary>
        /// The keyword field that holds each role a restricted item is limited to; absent on an unrestricted item.
        /// </summary>
        public const string RoleNamesField = "Content.ContentAccessControl.RoleNames";

        private readonly IContentDefinitionManager _contentDefinitionManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentAccessIndexHandler"/> class.
        /// </summary>
        /// <param name="contentDefinitionManager">The content definition manager.</param>
        public ContentAccessIndexHandler(IContentDefinitionManager contentDefinitionManager)
        {
            _contentDefinitionManager = contentDefinitionManager;
        }

        /// <inheritdoc/>
        public async Task BuildIndexAsync(BuildDocumentIndexContext context)
        {
            if (context?.Record is not ContentItem contentItem)
            {
                return;
            }

            foreach (var roleName in await ContentAccessRestrictions.GetRoleNamesAsync(contentItem, _contentDefinitionManager))
            {
                context.DocumentIndex.Set(RoleNamesField, roleName, DocumentIndexOptions.Keyword | DocumentIndexOptions.Store);
            }
        }
    }
}
