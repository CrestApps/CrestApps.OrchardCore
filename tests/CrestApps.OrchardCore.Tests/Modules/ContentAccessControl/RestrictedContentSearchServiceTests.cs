using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.ContentAccessControl.Search;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Indexing.Models;
using OrchardCore.Search.Abstractions;

namespace CrestApps.OrchardCore.Tests.Modules.ContentAccessControl;

/// <summary>
/// SEC-P8: the search page leaves out the hits the searcher may not view. These tests page through results the way the
/// pinned Orchard Core 4.0 preview does, with providers that read the last argument the way the real ones do:
/// <list type="bullet">
/// <item><c>SearchController</c> asks for <c>(0, PageSize + 1)</c>, then for <c>(After, After + PageSize + 1)</c>: an end
/// index. It loads the hits with a database query that keeps no rank order, takes <c>PageSize + 1</c> of them, sorts
/// those by rank, shows <c>PageSize</c> and offers a next page, at <c>After = size - 1</c>, when it took
/// <c>PageSize + 1</c>.</item>
/// <item><c>LuceneSearchQueryService</c> collects the top <c>end</c> hits and returns those from <c>start</c>.</item>
/// <item><c>ElasticsearchService</c> sends <c>From = start</c> and <c>Size = size</c>: a count.</item>
/// </list>
/// An id that starts with "r" is a restricted item.
/// </summary>
public sealed class RestrictedContentSearchServiceTests
{
    private static readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    [Fact]
    public async Task OnLucene_WithNothingRestricted_EveryPageIsWhatLuceneAloneReturns()
    {
        // Arrange
        var hits = Hits(31);
        var site = new SearchSite(hits, Provider.Lucene);

        // Act
        var pages = await site.PageThroughAsync(pageSize: 10);

        // Assert
        Assert.Equal(4, pages.Count);
        Assert.Equal(hits[..10], pages[0]);
        Assert.Equal(hits[10..20], pages[1]);
        Assert.Equal(hits[20..30], pages[2]);
        Assert.Equal(hits[30..], pages[3]);
    }

    [Fact]
    public async Task OnLucene_PagingThroughRestrictedHits_ShowsEveryPublicHitInRankOrder_AndNoRestrictedOne()
    {
        // Arrange
        var hits = Hits(40, restricted: [2, 9, 10, 11, 23, 24, 25, 26, 27, 39]);
        var site = new SearchSite(hits, Provider.Lucene);

        // Act
        var pages = await site.PageThroughAsync(pageSize: 5);

        // Assert
        var shown = pages.SelectMany(page => page).ToList();
        Assert.DoesNotContain(shown, id => id.StartsWith('r'));
        Assert.Equal(hits.Where(id => !id.StartsWith('r')), shown.Distinct());

        foreach (var page in pages)
        {
            Assert.Equal(page.OrderBy(id => Array.IndexOf(hits, id)), page);
        }
    }

    [Fact]
    public async Task OnLucene_APageWithARestrictedHit_StillOffersTheNextPage()
    {
        // Arrange: the first window (ranks 0 to 5) holds one restricted hit; more public hits follow it.
        var hits = Hits(12, restricted: [3]);
        var site = new SearchSite(hits, Provider.Lucene);

        // Act
        var (shown, after) = await site.SearchPageAsync(pageSize: 5, after: null);

        // Assert
        Assert.Equal(["o0", "o1", "o2", "o4", "o5"], shown);
        Assert.NotNull(after);
    }

    [Fact]
    public async Task OnLucene_ALaterPageWithARestrictedHit_StillOffersTheNextPage()
    {
        // Arrange: the round that refills page 2 starts past the window the controller asked for.
        var hits = Hits(30, restricted: [12]);
        var site = new SearchSite(hits, Provider.Lucene);

        // Act
        var (shown, after) = await site.SearchPageAsync(pageSize: 5, after: "10");

        // Assert
        Assert.Equal(["o10", "o11", "o13", "o14", "o15"], shown);
        Assert.NotNull(after);
    }

    [Fact]
    public async Task OnElasticsearch_WithNothingRestricted_EveryPageHoldsTheRanksTheControllerAskedFor()
    {
        // Arrange: Elasticsearch reads the end index as a count and returns more hits than the window.
        var hits = Hits(31);
        var site = new SearchSite(hits, Provider.Elasticsearch);

        // Act
        var pages = await site.PageThroughAsync(pageSize: 10);

        // Assert
        Assert.Equal(4, pages.Count);
        Assert.Equal(hits[10..20], pages[1]);
        Assert.Equal(hits[30..], pages[3]);
    }

    [Fact]
    public async Task ADeepPage_LoadsAndAuthorizesABoundedNumberOfItems()
    {
        // Arrange: ?after=5000 came from the query string of an anonymous visitor.
        var hits = Hits(10_000, restricted: [.. Enumerable.Range(5_000, 40)]);
        var site = new SearchSite(hits, Provider.Lucene);

        // Act
        var (shown, _) = await site.SearchPageAsync(pageSize: 10, after: "5000");

        // Assert
        Assert.All(shown, id => Assert.False(id.StartsWith('r')));
        Assert.InRange(site.LoadedItems, 1, RestrictedContentSearchService.MaxRounds * 11);
    }

    private static string[] Hits(int count, int[] restricted = null)
        => [.. Enumerable.Range(0, count).Select(rank => (restricted?.Contains(rank) == true ? "r" : "o") + rank.ToString(CultureInfo.InvariantCulture))];

    private enum Provider
    {
        Lucene,
        Elasticsearch,
    }

    private sealed class SearchSite
    {
        private readonly RestrictedContentSearchService _search;

        public SearchSite(string[] hits, Provider provider)
        {
            var inner = new Mock<ISearchService>();
            inner.Setup(service => service.SearchAsync(It.IsAny<IndexProfile>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((IndexProfile _, string _, int start, int last) => new SearchResult
                {
                    Success = true,
                    ContentItemIds = provider == Provider.Lucene
                        ? [.. hits.Skip(start).Take(Math.Max(0, last - start))]
                        : [.. hits.Skip(start).Take(last)],
                });

            var contents = new Mock<IContentManager>();
            contents.Setup(manager => manager.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<VersionOptions>()))
                .ReturnsAsync((IEnumerable<string> ids, VersionOptions _) =>
                {
                    var items = ids.Select(id => id.StartsWith('r') ? ContentAccessSurfacesTests.Restricted(id) : ContentAccessSurfacesTests.Open(id)).ToList();

                    LoadedItems += items.Count;

                    return items;
                });

            _search = new RestrictedContentSearchService(inner.Object, contents.Object, ContentAccessSurfacesTests.Authorization(), ContentAccessSurfacesTests.Accessor(_anonymous));
        }

        public int LoadedItems { get; private set; }

        public async Task<List<string[]>> PageThroughAsync(int pageSize)
        {
            var pages = new List<string[]>();
            string after = null;

            do
            {
                var (shown, next) = await SearchPageAsync(pageSize, after);

                pages.Add(shown);
                after = next;
            }
            while (after is not null && pages.Count < 100);

            return pages;
        }

        // SearchController.Search of the pinned package, from the pager arguments to the items it shows.
        public async Task<(string[] Shown, string After)> SearchPageAsync(int pageSize, string after)
        {
            var from = 0;
            var size = pageSize + 1;

            if (after is not null)
            {
                from = int.Parse(after, CultureInfo.InvariantCulture);
                size = from + pageSize + 1;
            }

            var result = await _search.SearchAsync(new IndexProfile(), "members", from, size);
            var ids = result.ContentItemIds ?? [];

            if (!result.Success || ids.Count == 0)
            {
                return ([], null);
            }

            // The database query has no rank order: it is modeled here as the reverse of the ranks.
            var loaded = ids.OrderByDescending(id => ids.IndexOf(id)).Take(pageSize + 1).ToList();
            var shown = loaded.OrderBy(id => ids.IndexOf(id)).Take(pageSize).ToArray();

            return (shown, loaded.Count == pageSize + 1 ? (size - 1).ToString(CultureInfo.InvariantCulture) : null);
        }
    }
}
