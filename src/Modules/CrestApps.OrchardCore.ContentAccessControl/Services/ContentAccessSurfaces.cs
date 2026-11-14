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
    /// title and summary don't reach an RSS reader outside its roles. A feed of a container the reader may not view (a
    /// members-only list or blog) is emptied, channel title and description included.
    /// </summary>
    public sealed class RestrictedContentFeedItemBuilder : IFeedItemBuilder
    {
        private readonly IFeedItemBuilder _inner;
        private readonly IContentManager _contentManager;
        private readonly IAuthorizationService _authorizationService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestrictedContentFeedItemBuilder"/> class.
        /// </summary>
        /// <param name="inner">The feed item builder it wraps.</param>
        /// <param name="contentManager">The content manager, for the feed's container.</param>
        /// <param name="authorizationService">The authorization service.</param>
        /// <param name="httpContextAccessor">The HTTP context accessor, for the reader.</param>
        public RestrictedContentFeedItemBuilder(IFeedItemBuilder inner, IContentManager contentManager, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
        {
            _inner = inner;
            _contentManager = contentManager;
            _authorizationService = authorizationService;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        public async Task PopulateAsync(FeedContext context)
        {
            await _inner.PopulateAsync(context);

            if (context?.Response is null)
            {
                return;
            }

            var user = _httpContextAccessor.HttpContext?.User ?? new System.Security.Claims.ClaimsPrincipal();

            // Orchard's list feed writes its container's title and description into the channel with no check of its own.
            if (await GetContainerAsync(context) is { } container &&
                !await _authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, container))
            {
                context.Response.Element?.RemoveNodes();
                context.Response.Items?.Clear();

                return;
            }

            if (context.Response.Items is not { Count: > 0 } items)
            {
                return;
            }

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

        // The container the list feed was asked for, bound the way Orchard's list feed binds it.
        private async Task<ContentItem> GetContainerAsync(FeedContext context)
        {
            var model = new FeedContainer();

            if (context.Updater is null || !await context.Updater.TryUpdateModelAsync(model) || string.IsNullOrEmpty(model.ContentItemId))
            {
                return null;
            }

            return await _contentManager.GetAsync(model.ContentItemId);
        }

        private sealed class FeedContainer
        {
            public string ContentItemId { get; set; }
        }
    }
}

namespace CrestApps.OrchardCore.ContentAccessControl.Search
{
    /// <summary>
    /// Wraps a search service and takes out of its results every content item the searcher may not view, so the search
    /// page never shows a restricted item's title or summary outside its roles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two numbers keep the meaning Orchard's search page gives them: <c>start</c> is the rank of the first hit and
    /// the last number the rank after the last hit. The page asks for <c>(0, PageSize + 1)</c>, then for
    /// <c>(After, After + PageSize + 1)</c>, and offers a next page, at that end minus one, only when it gets
    /// <c>PageSize + 1</c> hits back. Lucene reads the numbers the same way. Elasticsearch reads the end as a count and
    /// returns more, so what comes back is cut to the window that was asked for.
    /// </para>
    /// <para>
    /// When the window holds restricted hits, the hits right after it are read, a window at a time and at most
    /// <see cref="MaxRounds"/> reads in all, until the window is full again or the index has no more. A page with a
    /// restricted hit is then still full and still offers the next page, so no public hit becomes unreachable. The page
    /// boundaries stay where the search page puts them, so the hits that refilled a page show again at the top of the
    /// next one. A search loads and authorizes at most <see cref="MaxRounds"/> windows of items, whichever page is asked
    /// for. The total count is lowered by the hits taken out of what was read.
    /// </para>
    /// </remarks>
    public sealed class RestrictedContentSearchService : ISearchService
    {
        /// <summary>
        /// The most reads one search makes to fill a page.
        /// </summary>
        public const int MaxRounds = 5;

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
            start = Math.Max(0, start);

            var result = await _inner.SearchAsync(index, term, start, size);

            if (result?.ContentItemIds is not { Count: > 0 })
            {
                return result;
            }

            var user = _httpContextAccessor.HttpContext?.User ?? new System.Security.Claims.ClaimsPrincipal();
            var window = size - start;

            // Not a window the search page asks for: what came back is only filtered.
            var read = window > 0 ? result.ContentItemIds.Take(window).ToList() : [.. result.ContentItemIds];
            var allowed = await AllowedAsync(read, result.Latest, user);
            var removed = read.Count - allowed.Count;
            var next = start + read.Count;
            var more = window > 0 && read.Count == window;

            for (var round = 1; round < MaxRounds && more && allowed.Count < window; round++)
            {
                var batch = (await _inner.SearchAsync(index, term, next, next + window))?.ContentItemIds?.Take(window).ToList() ?? [];
                var kept = await AllowedAsync(batch, result.Latest, user);

                removed += batch.Count - kept.Count;
                allowed.AddRange(kept);
                next += batch.Count;

                // Fewer hits than a window: the index has no more.
                more = batch.Count == window;
            }

            result.TotalCount = Math.Max(0, result.TotalCount - removed);
            result.ContentItemIds = window > 0 ? [.. allowed.Take(window)] : allowed;

            return result;
        }

        private async Task<List<string>> AllowedAsync(List<string> ids, bool latest, System.Security.Claims.ClaimsPrincipal user)
        {
            if (ids.Count == 0)
            {
                return [];
            }

            var items = (await _contentManager.GetAsync(ids, latest ? VersionOptions.Latest : VersionOptions.Published))
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

            return allowed;
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
