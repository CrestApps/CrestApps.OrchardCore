using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Search;
using CrestApps.OrchardCore.Tests.Agent.Indexing;
using OrchardCore.Elasticsearch;
using OrchardCore.Elasticsearch.Models;
using OrchardCore.Entities;
using OrchardCore.Lucene.Models;
using OrchardCore.Search.Lucene;

namespace CrestApps.OrchardCore.Tests.Agent.Search;

/// <summary>
/// The tool that runs an ad-hoc JSON query against a Lucene or Elasticsearch index. It runs the query through the
/// query source of the provider, as the query API of the provider does, and never for an index it may not query.
/// </summary>
public sealed class RunSearchQueryToolTests
{
    private const string MatchAll = """{ "query": { "match_all": {} }, "size": {{ size }} }""";

    [Fact]
    public async Task RunSearchQuery_ForALuceneIndex_RunsAnUnsavedLuceneQueryAndReturnsTheDocuments()
    {
        // Arrange
        var harness = new SearchToolHarness();

        harness.Queries.Execute = (_, _) => new LuceneQueryResults
        {
            Count = 12,
            Items =
            [
                new JsonObject
                {
                    ["ContentItemId"] = "a",
                    ["Content.ContentItem.DisplayText"] = "Apple",
                },
                new JsonObject
                {
                    ["ContentItemId"] = "b",
                    ["Content.ContentItem.DisplayText"] = "Banana",
                },
            ],
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunSearchQueryTool(), services, new()
        {
            ["index"] = "blog-1",
            ["query"] = MatchAll,
            ["parameters"] = new JsonObject
            {
                ["size"] = 2,
            },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal("blog", json.GetProperty("index").GetString());
        Assert.Equal("Lucene", json.GetProperty("provider").GetString());
        Assert.Equal(12, json.GetProperty("matches").GetInt64());
        Assert.Equal(2, json.GetProperty("total").GetInt32());
        Assert.Equal(new[] { "Apple", "Banana" }, items.Select(item => item.GetProperty("Content.ContentItem.DisplayText").GetString()));

        // The query is not saved. It names the index profile and carries the template, and returns documents.
        var query = harness.Queries.Executed;

        Assert.Equal("Lucene", query.Source);
        Assert.False(query.ReturnContentItems);
        Assert.True(query.TryGet<LuceneQueryMetadata>(out var metadata));
        Assert.Equal("blog", metadata.Index);
        Assert.Equal(MatchAll, metadata.Template);
        Assert.Equal("2", harness.Queries.ExecutedParameters["size"].ToString());
        Assert.Empty(harness.Queries.Queries);
    }

    [Fact]
    public async Task RunSearchQuery_ForAnElasticsearchIndex_RunsAnUnsavedElasticsearchQuery()
    {
        // Arrange
        var harness = new SearchToolHarness();

        harness.Queries.Execute = (_, _) => new ElasticsearchQueryResults
        {
            Count = 1,
            Items =
            [
                new JsonObject
                {
                    ["ContentItemId"] = "p",
                },
            ],
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunSearchQueryTool(), services, new()
        {
            ["index"] = "products",
            ["query"] = new JsonObject
            {
                ["query"] = new JsonObject
                {
                    ["match_all"] = new JsonObject(),
                },
            },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var query = harness.Queries.Executed;

        Assert.Equal("Elasticsearch", json.GetProperty("provider").GetString());
        Assert.Equal(1, json.GetProperty("matches").GetInt64());
        Assert.Equal("Elasticsearch", query.Source);
        Assert.True(query.TryGet<ElasticsearchQueryMetadata>(out var metadata));
        Assert.Equal("products", metadata.Index);

        // A query given as a JSON object is sent as JSON text.
        Assert.Equal("""{"query":{"match_all":{}}}""", metadata.Template);
    }

    [Fact]
    public async Task RunSearchQuery_WhenElasticsearchIsNotConfigured_RunsNothing()
    {
        // Arrange
        var harness = new SearchToolHarness
        {
            ElasticsearchUrl = null,
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunSearchQueryTool(), services, new()
        {
            ["index"] = "products",
            ["query"] = "{}",
        });

        // Assert
        Assert.Equal("The query was not run: Elasticsearch is not configured for this site.", result);
        Assert.Empty(harness.Queries.Calls);
    }

    [Theory]
    [InlineData("missing", "there is no index profile with the ID or the name 'missing'")]
    [InlineData("site", "the index 'site' belongs to the provider 'AzureAISearch', which takes no JSON query")]
    public async Task RunSearchQuery_WhenTheIndexCannotBeQueried_RunsNothing(string index, string expected)
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunSearchQueryTool(), services, new()
        {
            ["index"] = index,
            ["query"] = "{}",
        });

        // Assert
        Assert.StartsWith("The query was not run: ", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(harness.Queries.Calls);
    }

    [Fact]
    public async Task RunSearchQuery_WhenThePhysicalIndexIsMissing_RunsNothing()
    {
        // Arrange
        var harness = new SearchToolHarness();

        harness.LuceneIndexes.Exists = false;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunSearchQueryTool(), services, new()
        {
            ["index"] = "blog",
            ["query"] = "{}",
        });

        // Assert
        Assert.Equal("The query was not run: the index profile 'blog' has no physical index. Rebuild it with rebuildIndexProfile.", result);
        Assert.Empty(harness.Queries.Calls);
    }

    [Fact]
    public async Task RunSearchQuery_WhenTheQueryIsNotValid_ReturnsTheReasonAsTheRunQueryPageDoes()
    {
        // Arrange
        var harness = new SearchToolHarness();

        harness.Queries.Execute = (_, _) => throw new InvalidOperationException("Unexpected token 'matchall'.");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunSearchQueryTool(), services, new()
        {
            ["index"] = "blog",
            ["query"] = """{ "query": { "matchall": {} } }""",
        });

        // Assert
        Assert.Equal("The query failed: Unexpected token 'matchall'.", result);
    }

    [Theory]
    [InlineData(null, "{}", "The index argument is required")]
    [InlineData("blog", null, "The query argument is required")]
    [InlineData("blog", "  ", "The query argument is required")]
    public async Task RunSearchQuery_WithoutAnIndexOrAQuery_RunsNothing(string index, string query, string expected)
    {
        // Arrange
        var harness = new SearchToolHarness();

        using var services = harness.Build();

        var arguments = new Dictionary<string, object>();

        if (index is not null)
        {
            arguments["index"] = index;
        }

        if (query is not null)
        {
            arguments["query"] = query;
        }

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunSearchQueryTool(), services, arguments);

        // Assert
        Assert.StartsWith(expected, result, StringComparison.Ordinal);
        Assert.Empty(harness.Queries.Calls);
    }
}
