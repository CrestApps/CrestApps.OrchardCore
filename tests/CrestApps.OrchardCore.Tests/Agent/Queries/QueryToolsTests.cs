using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Queries;
using CrestApps.OrchardCore.Tests.Agent.Indexing;
using OrchardCore.ContentManagement;
using OrchardCore.Search.Lucene;

namespace CrestApps.OrchardCore.Tests.Agent.Queries;

/// <summary>
/// The tools that list and run saved queries. They go through the query manager the Queries admin page and the query
/// API use, and the rows a query returns are bounded.
/// </summary>
public sealed class QueryToolsTests
{
    private const string MatchAll = """{ "query": { "match_all": {} }, "size": 5 }""";

    [Fact]
    public async Task ListQueries_ReturnsTheQueriesWithTheirTemplateAndIndexAndTheEnabledSources()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListQueriesTool(), services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(2, json.GetProperty("total").GetInt32());
        Assert.Equal(new[] { "ContentCounts", "RecentPosts" }, items.Select(item => item.GetProperty("name").GetString()));
        Assert.Equal(new[] { "Elasticsearch", "Lucene", "Sql" }, json.GetProperty("sources").EnumerateArray().Select(source => source.GetString()));

        var sql = items[0];

        Assert.Equal("Sql", sql.GetProperty("source").GetString());
        Assert.Equal("SELECT ContentType FROM ContentItemIndex", sql.GetProperty("template").GetString());
        Assert.False(sql.TryGetProperty("index", out _));

        var lucene = items[1];

        Assert.Equal("Lucene", lucene.GetProperty("source").GetString());
        Assert.Equal("blog", lucene.GetProperty("index").GetString());
        Assert.Equal(MatchAll, lucene.GetProperty("template").GetString());
        Assert.True(lucene.GetProperty("returnContentItems").GetBoolean());
        Assert.Equal("""{ "type": "object" }""", lucene.GetProperty("schema").GetString());
    }

    [Fact]
    public async Task ListQueries_WithASearch_KeepsTheQueriesWhoseNameHasTheText()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListQueriesTool(), services, new()
        {
            ["search"] = "recent",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("RecentPosts", Assert.Single(json.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal(1, json.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task RunQuery_RunsTheSavedQueryWithItsParametersAndReturnsContentItemsAsSummaries()
    {
        // Arrange
        var harness = CreateHarness();

        harness.Queries.Execute = (_, _) => new LuceneQueryResults
        {
            Count = 40,
            Items =
            [
                new ContentItem
                {
                    ContentItemId = "a",
                    ContentItemVersionId = "a-1",
                    ContentType = "BlogPost",
                    DisplayText = "Apple",
                    Published = true,
                    Latest = true,
                },
            ],
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunQueryTool(), services, new()
        {
            ["name"] = "recentposts",
            ["parameters"] = """{ "size": 5 }""",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());

        Assert.Equal("RecentPosts", json.GetProperty("name").GetString());
        Assert.Equal("Lucene", json.GetProperty("source").GetString());
        Assert.Equal(40, json.GetProperty("matches").GetInt64());
        Assert.Equal(1, json.GetProperty("total").GetInt32());
        Assert.Equal("a", item.GetProperty("contentItemId").GetString());
        Assert.Equal("Apple", item.GetProperty("displayText").GetString());
        Assert.Equal("BlogPost", item.GetProperty("contentType").GetString());

        // The parts and fields of a content item are not returned.
        Assert.False(item.TryGetProperty("Content", out _));
        Assert.Same(harness.Queries.Queries["RecentPosts"], harness.Queries.Executed);
        Assert.Equal("5", harness.Queries.ExecutedParameters["size"].ToString());
    }

    [Fact]
    public async Task RunQuery_WhenThereIsNoSuchQuery_SaysSo()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunQueryTool(), services, new()
        {
            ["name"] = "Missing",
        });

        // Assert
        Assert.StartsWith("The query was not run: there is no saved query named 'Missing'.", result, StringComparison.Ordinal);
        Assert.Empty(harness.Queries.Calls);
    }

    [Fact]
    public async Task RunQuery_WhenTheQueryFails_DoesNotReturnTheError()
    {
        // Arrange
        var harness = CreateHarness();

        harness.Queries.Execute = (_, _) => throw new InvalidOperationException("Invalid column name 'Secret'.");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunQueryTool(), services, new()
        {
            ["name"] = "ContentCounts",
        });

        // Assert
        Assert.StartsWith("The query 'ContentCounts' failed with an error.", result, StringComparison.Ordinal);

        // The caller may run the query without being allowed to read it, so the database error is not relayed.
        Assert.DoesNotContain("Secret", result, StringComparison.Ordinal);
    }

    private static SearchToolHarness CreateHarness()
    {
        var harness = new SearchToolHarness();
        var recentPosts = harness.Queries.Add("RecentPosts", "Lucene", MatchAll, "blog");

        recentPosts.Schema = """{ "type": "object" }""";
        recentPosts.ReturnContentItems = true;

        harness.Queries.Add("ContentCounts", "Sql", "SELECT ContentType FROM ContentItemIndex");

        return harness;
    }
}
