using CrestApps.OrchardCore.AI.Agent.Search;
using CrestApps.OrchardCore.Tests.Agent.Indexing;
using OrchardCore.ContentManagement;
using OrchardCore.Search.Abstractions;

namespace CrestApps.OrchardCore.Tests.Agent.Search;

/// <summary>
/// The tool that searches an index the way the search page of the site does: the default index of the site search
/// settings or a named one, the search service of the provider, and the content items in the order of the results.
/// </summary>
public sealed class SearchIndexToolTests
{
    [Fact]
    public async Task SearchIndex_WithoutAnIndex_SearchesTheDefaultIndexAndReturnsTheItemsInResultOrder()
    {
        // Arrange
        var harness = CreateHarness();

        harness.LuceneSearch.Result = new SearchResult
        {
            Success = true,
            ContentItemIds = ["c", "a", "gone", "b"],
            Highlights = new()
            {
                ["c"] = new Dictionary<string, IReadOnlyCollection<string>>
                {
                    ["Content.ContentItem.FullText"] = ["An <em>orchard</em> in spring", "the <em>orchard</em> gate"],
                },
            },
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SearchIndexTool(), services, new()
        {
            ["terms"] = " orchard ",
            ["pageSize"] = 3,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal("blog", json.GetProperty("index").GetString());
        Assert.Equal("Lucene", json.GetProperty("provider").GetString());
        Assert.Equal("orchard", json.GetProperty("terms").GetString());

        // The first three results make the page; the content store returned them in another order.
        Assert.Equal(new[] { "c", "a" }, items.Select(item => item.GetProperty("contentItemId").GetString()));
        Assert.Equal("Cherry", items[0].GetProperty("displayText").GetString());
        Assert.Equal("BlogPost", items[0].GetProperty("contentType").GetString());
        Assert.Equal(
            new[] { "An <em>orchard</em> in spring", "the <em>orchard</em> gate" },
            items[0].GetProperty("highlights").GetProperty("Content.ContentItem.FullText").EnumerateArray().Select(fragment => fragment.GetString()));
        Assert.False(items[1].TryGetProperty("highlights", out _));

        // One more result than a page was asked for and came back, and one result is not in the content store.
        Assert.True(json.GetProperty("hasMore").GetBoolean());
        Assert.Equal(1, json.GetProperty("notInContentStore").GetInt32());
        Assert.Equal(1, json.GetProperty("page").GetInt32());
        Assert.Equal(3, json.GetProperty("pageSize").GetInt32());
        Assert.Equal(("blog", "orchard", 0, 4), Assert.Single(harness.LuceneSearch.Searches));
    }

    [Fact]
    public async Task SearchIndex_ForTheSecondPage_AsksForTheResultsAfterTheFirstPageAsTheSearchPageDoes()
    {
        // Arrange
        var harness = CreateHarness();

        harness.LuceneSearch.Result = new SearchResult
        {
            Success = true,
            ContentItemIds = ["b"],
            TotalCount = 3,
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SearchIndexTool(), services, new()
        {
            ["terms"] = "orchard",
            ["index"] = "blog",
            ["page"] = 2,
            ["pageSize"] = 2,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("b", Assert.Single(json.GetProperty("items").EnumerateArray()).GetProperty("contentItemId").GetString());
        Assert.False(json.GetProperty("hasMore").GetBoolean());
        Assert.Equal(3, json.GetProperty("total").GetInt64());
        Assert.Equal(("blog", "orchard", 2, 5), Assert.Single(harness.LuceneSearch.Searches));
    }

    [Fact]
    public async Task SearchIndex_WithoutAnIndexAndWithoutADefaultOne_SaysSo()
    {
        // Arrange
        var harness = CreateHarness();

        harness.DefaultSearchIndex = null;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SearchIndexTool(), services, new()
        {
            ["terms"] = "orchard",
        });

        // Assert
        Assert.StartsWith("The search was not run: no default search index is configured", result, StringComparison.Ordinal);
        Assert.Empty(harness.LuceneSearch.Searches);
    }

    [Theory]
    [InlineData("missing", "there is no index profile named 'missing'")]
    [InlineData("products", "the provider 'Elasticsearch' of the index 'products' has no search service")]
    public async Task SearchIndex_WhenTheIndexCannotBeSearched_SaysWhy(string index, string expected)
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SearchIndexTool(), services, new()
        {
            ["terms"] = "orchard",
            ["index"] = index,
        });

        // Assert
        Assert.StartsWith("The search was not run: ", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(harness.LuceneSearch.Searches);
    }

    [Fact]
    public async Task SearchIndex_WhenTheProviderCannotSearch_SaysSoInsteadOfReturningNoResults()
    {
        // Arrange
        var harness = CreateHarness();

        harness.LuceneSearch.Result = new SearchResult
        {
            Success = false,
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SearchIndexTool(), services, new()
        {
            ["terms"] = "orchard AND (",
        });

        // Assert
        Assert.StartsWith("The search was not run: the provider 'Lucene' could not search the index 'blog'.", result, StringComparison.Ordinal);
    }

    private static SearchToolHarness CreateHarness()
    {
        var harness = new SearchToolHarness
        {
            DefaultSearchIndex = "blog",
        };

        harness.ContentItems.AddRange(
        [
            CreateContentItem("a", "Apple"),
            CreateContentItem("b", "Banana"),
            CreateContentItem("c", "Cherry"),
        ]);

        return harness;
    }

    private static ContentItem CreateContentItem(string contentItemId, string displayText)
    {
        return new ContentItem
        {
            ContentItemId = contentItemId,
            ContentType = "BlogPost",
            DisplayText = displayText,
            Published = true,
        };
    }
}
