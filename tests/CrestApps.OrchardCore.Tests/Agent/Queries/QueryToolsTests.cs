using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Queries;
using CrestApps.OrchardCore.Tests.Agent.Indexing;
using OrchardCore.ContentManagement;
using OrchardCore.Search.Lucene;

namespace CrestApps.OrchardCore.Tests.Agent.Queries;

/// <summary>
/// The tools that list, save, delete and run saved queries and run an ad-hoc SQL query. They go through the query
/// manager the Queries admin page and the query API use, a refused call changes nothing, SQL is only ever run by the
/// SQL query source of Orchard Core, and the rows a query returns are bounded.
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
    public async Task SaveQuery_ForANewName_CreatesTheQueryOfTheSource()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveQueryTool(), services, new()
        {
            ["name"] = "LatestArticles",
            ["source"] = "lucene",
            ["template"] = MatchAll,
            ["index"] = "BLOG",
            ["returnContentItems"] = true,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var saved = harness.Queries.Queries["LatestArticles"];

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal("LatestArticles", json.GetProperty("query").GetProperty("name").GetString());
        Assert.Equal("Lucene", saved.Source);
        Assert.True(saved.ReturnContentItems);
        Assert.Equal(MatchAll, FakeQueryManager.GetTemplate(saved));

        // The index is stored by the name of its profile.
        Assert.Equal("blog", FakeQueryManager.GetIndex(saved));
        Assert.Equal(new[] { "Save:LatestArticles" }, harness.Queries.Calls);
    }

    [Theory]
    [InlineData("Sql", "SELECT 1", null, "this tool does not save SQL queries")]
    [InlineData("Graph", MatchAll, "blog", "the query source 'Graph' is not enabled on this site")]
    [InlineData("Lucene", MatchAll, "products", "the index 'products' belongs to the provider 'Elasticsearch'")]
    [InlineData("Lucene", MatchAll, "missing", "there is no index profile named 'missing'")]
    [InlineData("Lucene", MatchAll, null, "template and index are required to create a query")]
    [InlineData("Lucene", null, "blog", "template and index are required to create a query")]
    public async Task SaveQuery_WhenANewQueryIsNotValid_SavesNothing(string source, string template, string index, string expected)
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        var arguments = new Dictionary<string, object>
        {
            ["name"] = "LatestArticles",
            ["source"] = source,
        };

        if (template is not null)
        {
            arguments["template"] = template;
        }

        if (index is not null)
        {
            arguments["index"] = index;
        }

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveQueryTool(), services, arguments);

        // Assert
        Assert.StartsWith("Nothing was saved: ", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(harness.Queries.Calls);
        Assert.False(harness.Queries.Queries.ContainsKey("LatestArticles"));
    }

    [Theory]
    [InlineData("Latest Articles")]
    [InlineData("latest-articles")]
    [InlineData("1Latest")]
    public async Task SaveQuery_WithANameThatIsNotATechnicalName_SavesNothing(string name)
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveQueryTool(), services, new()
        {
            ["name"] = name,
            ["source"] = "Lucene",
            ["template"] = MatchAll,
            ["index"] = "blog",
        });

        // Assert
        Assert.StartsWith($"Nothing was saved: the name '{name}' cannot be used.", result, StringComparison.Ordinal);
        Assert.Empty(harness.Queries.Calls);
    }

    [Fact]
    public async Task SaveQuery_ForAnExistingName_ChangesTheGivenFieldsAndKeepsTheOthers()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveQueryTool(), services, new()
        {
            ["name"] = "recentposts",
            ["template"] = """{ "query": { "match_all": {} }, "size": 10 }""",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var saved = harness.Queries.Queries["RecentPosts"];

        Assert.False(json.GetProperty("created").GetBoolean());
        Assert.Equal("""{ "query": { "match_all": {} }, "size": 10 }""", FakeQueryManager.GetTemplate(saved));

        // What was not given stays: the index, the schema and the return-content-items flag.
        Assert.Equal("blog", FakeQueryManager.GetIndex(saved));
        Assert.Equal("""{ "type": "object" }""", saved.Schema);
        Assert.True(saved.ReturnContentItems);

        // The Edit action of the Queries page removes the stored query and saves it again.
        Assert.Equal(new[] { "Delete:RecentPosts", "Update:RecentPosts" }, harness.Queries.Calls);
    }

    [Fact]
    public async Task SaveQuery_WithANewName_RenamesTheQuery()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveQueryTool(), services, new()
        {
            ["name"] = "RecentPosts",
            ["newName"] = "LatestPosts",
            ["schema"] = string.Empty,
        });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("saved").GetBoolean());
        Assert.False(harness.Queries.Queries.ContainsKey("RecentPosts"));
        Assert.Equal(string.Empty, harness.Queries.Queries["LatestPosts"].Schema);
        Assert.Equal(new[] { "Delete:RecentPosts", "Update:LatestPosts" }, harness.Queries.Calls);
    }

    [Theory]
    [InlineData("ContentCounts", "template", "SELECT 2", "this tool does not save SQL queries")]
    [InlineData("RecentPosts", "source", "Elasticsearch", "the source of the saved query 'RecentPosts' is 'Lucene' and cannot be changed")]
    [InlineData("RecentPosts", "newName", "ContentCounts", "a query named 'ContentCounts' already exists")]
    [InlineData("RecentPosts", "newName", "Recent Posts", "the name 'Recent Posts' cannot be used")]
    [InlineData("RecentPosts", "schema", "{ not json", "schema must be JSON text")]
    [InlineData("RecentPosts", "template", " ", "the template of a query cannot be empty")]
    [InlineData("RecentPosts", "index", "products", "the index 'products' belongs to the provider 'Elasticsearch'")]
    public async Task SaveQuery_WhenAnUpdateIsNotValid_LeavesTheQueryAsItWas(string name, string argument, string value, string expected)
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveQueryTool(), services, new()
        {
            ["name"] = name,
            [argument] = value,
        });

        // Assert
        Assert.StartsWith("Nothing was saved: ", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(harness.Queries.Calls);
        Assert.Equal(MatchAll, FakeQueryManager.GetTemplate(harness.Queries.Queries["RecentPosts"]));
        Assert.Equal("SELECT ContentType FROM ContentItemIndex", FakeQueryManager.GetTemplate(harness.Queries.Queries["ContentCounts"]));
    }

    [Fact]
    public async Task DeleteQuery_DeletesTheNamedQueries()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteQueryTool(), services, new()
        {
            ["names"] = new[] { "recentposts", "ContentCounts", "RecentPosts" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(new[] { "RecentPosts", "ContentCounts" }, json.GetProperty("deleted").EnumerateArray().Select(name => name.GetString()));
        Assert.Equal(2, json.GetProperty("deletedCount").GetInt32());
        Assert.Empty(harness.Queries.Queries);
        Assert.Equal(new[] { "Delete:RecentPosts,ContentCounts" }, harness.Queries.Calls);
    }

    [Fact]
    public async Task DeleteQuery_WhenOneOfTheNamesIsUnknown_DeletesNothing()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteQueryTool(), services, new()
        {
            ["names"] = new[] { "RecentPosts", "Missing" },
        });

        // Assert
        Assert.StartsWith("Nothing was deleted: there is no saved query named 'Missing'.", result, StringComparison.Ordinal);
        Assert.Empty(harness.Queries.Calls);
        Assert.Equal(2, harness.Queries.Queries.Count);
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

    [Fact]
    public async Task RunSqlQuery_RunsTheSqlThroughTheSqlQuerySourceAndReturnsABoundedPageOfRows()
    {
        // Arrange
        var harness = CreateHarness();

        harness.Queries.Execute = (_, _) => new FakeQueryManager.Results
        {
            Items = Enumerable.Range(1, 30)
                .Select(number => (object)new JsonObject
                {
                    ["DocumentId"] = number,
                    ["ContentType"] = "BlogPost",
                })
                .ToArray(),
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunSqlQueryTool(), services, new()
        {
            ["query"] = "SELECT DocumentId, ContentType FROM ContentItemIndex WHERE ContentType = @type LIMIT 30",
            ["parameters"] = new JsonObject
            {
                ["type"] = "BlogPost",
            },
            ["page"] = 2,
            ["pageSize"] = 25,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();
        var query = harness.Queries.Executed;

        Assert.Equal(30, json.GetProperty("total").GetInt32());
        Assert.Equal(2, json.GetProperty("page").GetInt32());
        Assert.Equal(25, json.GetProperty("pageSize").GetInt32());
        Assert.Equal(Enumerable.Range(26, 5), items.Select(item => item.GetProperty("DocumentId").GetInt32()));
        Assert.False(json.TryGetProperty("notes", out _));

        // The SQL is the template of an unsaved query of the SQL source, which parses it before it is run; the tool
        // itself opens no connection.
        Assert.Equal("Sql", query.Source);
        Assert.Null(query.Name);
        Assert.False(query.ReturnContentItems);
        Assert.Equal("SELECT DocumentId, ContentType FROM ContentItemIndex WHERE ContentType = @type LIMIT 30", FakeQueryManager.GetTemplate(query));
        Assert.Equal("BlogPost", harness.Queries.ExecutedParameters["type"].ToString());
        Assert.Equal(2, harness.Queries.Queries.Count);
    }

    [Fact]
    public async Task RunSqlQuery_LeavesOutValuesNamedLikeACredentialAlsoInsideAStoredDocument()
    {
        // Arrange
        var harness = CreateHarness();

        harness.Queries.Execute = (_, _) => new FakeQueryManager.Results
        {
            Items =
            [
                new JsonObject
                {
                    ["Id"] = 7,
                    ["Type"] = "OrchardCore.Users.Models.User, OrchardCore.Users.Core",
                    ["Content"] = """{"UserName":"admin","PasswordHash":"AQAAAAIAAYagAAAAEJ","SecurityStamp":"5WJ7","Properties":{"ApiKey":"k-123","Phone":"555"}}""",
                    ["PasswordHash"] = "AQAAAAIAAYagAAAAEJ",
                },
            ],
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunSqlQueryTool(), services, new()
        {
            ["query"] = "SELECT * FROM Document LIMIT 1",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var row = Assert.Single(json.GetProperty("items").EnumerateArray());
        var content = row.GetProperty("Content").GetString();

        Assert.DoesNotContain("AQAAAAIAAYagAAAAEJ", result, StringComparison.Ordinal);
        Assert.DoesNotContain("5WJ7", result, StringComparison.Ordinal);
        Assert.DoesNotContain("k-123", result, StringComparison.Ordinal);
        Assert.Equal("[redacted]", row.GetProperty("PasswordHash").GetString());
        Assert.Contains("\"UserName\":\"admin\"", content, StringComparison.Ordinal);
        Assert.Contains("\"PasswordHash\":\"[redacted]\"", content, StringComparison.Ordinal);
        Assert.Contains("\"Phone\":\"555\"", content, StringComparison.Ordinal);
        Assert.Contains(json.GetProperty("notes").EnumerateArray(), note => note.GetString().Contains("credential", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunSqlQuery_WhenNoRowsComeBack_SaysThatARefusedQueryLooksTheSame()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunSqlQueryTool(), services, new()
        {
            ["query"] = "DELETE FROM Document",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(0, json.GetProperty("total").GetInt32());
        Assert.Empty(json.GetProperty("items").EnumerateArray());
        Assert.Contains("when its SQL parser refuses the query", Assert.Single(json.GetProperty("notes").EnumerateArray()).GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunSqlQuery_WhenTheDatabaseRefusesTheQuery_ReturnsTheErrorAsTheRunSqlQueryPageDoes()
    {
        // Arrange
        var harness = CreateHarness();

        harness.Queries.Execute = (_, _) => throw new InvalidOperationException("Invalid object name 'Missing'.");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunSqlQueryTool(), services, new()
        {
            ["query"] = "SELECT * FROM Missing",
        });

        // Assert
        Assert.Equal("The query failed: Invalid object name 'Missing'.", result);
    }

    [Fact]
    public async Task RunSqlQuery_WhenTheSqlQuerySourceIsOff_RunsNothing()
    {
        // Arrange
        var harness = CreateHarness();

        harness.Queries.Sources.Remove("Sql");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunSqlQueryTool(), services, new()
        {
            ["query"] = "SELECT 1",
        });

        // Assert
        Assert.Equal("The query was not run: the SQL query source is not available on this site.", result);
        Assert.Empty(harness.Queries.Calls);
    }

    [Theory]
    [InlineData(null, null, "The query argument is required")]
    [InlineData("  ", null, "The query argument is required")]
    [InlineData("SELECT 1", "[1, 2]", "The query was not run: parameters must be a JSON object.")]
    public async Task RunSqlQuery_WithArgumentsItCannotUse_RunsNothing(string query, string parameters, string expected)
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        var arguments = new Dictionary<string, object>();

        if (query is not null)
        {
            arguments["query"] = query;
        }

        if (parameters is not null)
        {
            arguments["parameters"] = parameters;
        }

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunSqlQueryTool(), services, arguments);

        // Assert
        Assert.StartsWith(expected, result, StringComparison.Ordinal);
        Assert.Empty(harness.Queries.Calls);
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
