using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Indexing;
using CrestApps.OrchardCore.AI.Agent.Queries;
using CrestApps.OrchardCore.AI.Agent.Search;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Tests.Agent.Indexing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Agent;

/// <summary>
/// Every tool for search indexes, searches and queries acts for the person in the chat: it refuses a caller who is
/// not signed in and a caller who does not hold the permission the matching admin action asks for, and it changes and
/// runs nothing when it refuses. Each JSON schema takes no argument it does not declare and describes each argument.
/// </summary>
public sealed class OrchardSearchToolCallerTests
{
    public static TheoryData<string> ToolNames =>
    [
        ListIndexProfilesTool.TheName,
        GetIndexProfileTool.TheName,
        SaveIndexProfileTool.TheName,
        SynchronizeIndexProfileTool.TheName,
        RebuildIndexProfileTool.TheName,
        DeleteIndexProfileTool.TheName,
        SearchIndexTool.TheName,
        RunSearchQueryTool.TheName,
        ListQueriesTool.TheName,
        SaveQueryTool.TheName,
        DeleteQueryTool.TheName,
        RunQueryTool.TheName,
        RunSqlQueryTool.TheName,
    ];

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task InvokeAsync_WhenNobodyIsSignedIn_RefusesAndChangesNothing(string toolName)
    {
        // Arrange
        var harness = CreateHarness();

        harness.SignedIn = false;

        using var services = harness.Build();

        var (tool, arguments) = CreateTool(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        harness.VerifyNothingChangedOrRan();
    }

    [Theory]
    [InlineData(ListIndexProfilesTool.TheName, "ManageIndexes")]
    [InlineData(GetIndexProfileTool.TheName, "ManageIndexes")]
    [InlineData(SaveIndexProfileTool.TheName, "ManageIndexes")]
    [InlineData(SynchronizeIndexProfileTool.TheName, "ManageIndexes")]
    [InlineData(RebuildIndexProfileTool.TheName, "ManageIndexes")]
    [InlineData(DeleteIndexProfileTool.TheName, "ManageIndexes")]
    [InlineData(ListQueriesTool.TheName, "ManageQueries")]
    [InlineData(SaveQueryTool.TheName, "ManageQueries")]
    [InlineData(DeleteQueryTool.TheName, "ManageQueries")]
    [InlineData(RunQueryTool.TheName, "ExecuteApi_RecentPosts")]
    [InlineData(RunSqlQueryTool.TheName, "ManageSqlQueries")]
    public async Task InvokeAsync_WhenTheCallerLacksThePermissionOfTheAdminAction_RefusesAndChangesNothing(string toolName, string permission)
    {
        // Arrange
        var harness = CreateHarness();

        harness.Authorize = (_, _) => false;

        using var services = harness.Build();

        var (tool, arguments) = CreateTool(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal(permission, check.Permission);
        Assert.Null(check.Resource);
        harness.VerifyNothingChangedOrRan();
    }

    [Fact]
    public async Task SearchIndex_WhenTheCallerMayNotQueryTheIndex_RefusesAndSearchesNothing()
    {
        // Arrange
        var harness = CreateHarness();

        harness.Authorize = (_, _) => false;

        using var services = harness.Build();

        var (tool, arguments) = CreateTool(SearchIndexTool.TheName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal("QuerySearchIndex", check.Permission);

        // As on the search page, the permission is checked on the index profile, so the permission of one index is
        // enough to search it.
        Assert.Same(harness.Blog, check.Resource);
        harness.VerifyNothingChangedOrRan();
    }

    [Fact]
    public async Task RunSearchQuery_WhenTheCallerHoldsNoRunQueryPermission_RefusesBeforeLookingUpTheIndex()
    {
        // Arrange
        var harness = CreateHarness();

        harness.Authorize = (_, _) => false;

        using var services = harness.Build();

        var (tool, arguments) = CreateTool(RunSearchQueryTool.TheName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        var checks = services.GetRequiredService<FakeAuthorizationService>().Checks;

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal(new[] { "ManageLuceneIndexes", "ManageElasticIndexes" }, checks.Select(check => check.Permission));
        Assert.All(checks, check => Assert.Null(check.Resource));
        harness.VerifyNothingChangedOrRan();
    }

    [Fact]
    public async Task RunSearchQuery_WhenTheCallerHoldsThePermissionOfAnotherProvider_RefusesAndRunsNothing()
    {
        // Arrange
        var harness = CreateHarness();

        // The caller may run Lucene queries, and asks for an Elasticsearch index.
        harness.Authorize = (permission, _) => permission.Name == "ManageLuceneIndexes";

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunSearchQueryTool(), services, new()
        {
            ["index"] = "products",
            ["query"] = """{ "query": { "match_all": {} } }""",
        });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        harness.VerifyNothingChangedOrRan();
    }

    [Fact]
    public async Task RunQuery_ForANameThatMatchesNoQuery_ChecksThePermissionOfThatNameFirst()
    {
        // Arrange
        var harness = CreateHarness();

        harness.Authorize = (_, _) => false;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunQueryTool(), services, new()
        {
            ["name"] = "Missing",
        });

        // Assert
        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);

        // A caller who may not run queries gets the same refusal for a name that exists and one that does not.
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal("ExecuteApi_Missing", check.Permission);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void JsonSchema_ClosesTheArgumentsAndDescribesEachOne(string toolName)
    {
        // Arrange
        var (tool, _) = CreateTool(toolName);

        // Act
        var schema = tool.JsonSchema;

        // Assert
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal(JsonValueKind.False, schema.GetProperty("additionalProperties").ValueKind);
        Assert.Equal(JsonValueKind.Array, schema.GetProperty("required").ValueKind);

        var properties = schema.GetProperty("properties").EnumerateObject().ToList();

        Assert.All(properties, property => Assert.False(string.IsNullOrWhiteSpace(property.Value.GetProperty("description").GetString())));
        Assert.All(schema.GetProperty("required").EnumerateArray(), required => Assert.Contains(properties, property => property.Name == required.GetString()));
    }

    private static SearchToolHarness CreateHarness()
    {
        var harness = new SearchToolHarness
        {
            DefaultSearchIndex = "blog",
        };

        harness.Queries.Add("RecentPosts", "Lucene", """{ "query": { "match_all": {} } }""", "blog");

        return harness;
    }

    private static (AIFunction Tool, Dictionary<string, object> Arguments) CreateTool(string toolName)
    {
        return toolName switch
        {
            ListIndexProfilesTool.TheName => (new ListIndexProfilesTool(), []),
            GetIndexProfileTool.TheName => (new GetIndexProfileTool(), new() { ["index"] = "blog" }),
            SaveIndexProfileTool.TheName => (new SaveIndexProfileTool(), new() { ["name"] = "blog", ["indexedContentTypes"] = new[] { "Article" } }),
            SynchronizeIndexProfileTool.TheName => (new SynchronizeIndexProfileTool(), new() { ["indexes"] = new[] { "blog" }, ["reset"] = true }),
            RebuildIndexProfileTool.TheName => (new RebuildIndexProfileTool(), new() { ["indexes"] = new[] { "blog" } }),
            DeleteIndexProfileTool.TheName => (new DeleteIndexProfileTool(), new() { ["indexes"] = new[] { "blog" } }),
            SearchIndexTool.TheName => (new SearchIndexTool(), new() { ["terms"] = "orchard" }),
            RunSearchQueryTool.TheName => (new RunSearchQueryTool(), new() { ["index"] = "blog", ["query"] = """{ "query": { "match_all": {} } }""" }),
            ListQueriesTool.TheName => (new ListQueriesTool(), []),
            SaveQueryTool.TheName => (new SaveQueryTool(), new() { ["name"] = "RecentPosts", ["template"] = """{ "query": { "match_all": {} }, "size": 5 }""" }),
            DeleteQueryTool.TheName => (new DeleteQueryTool(), new() { ["names"] = new[] { "RecentPosts" } }),
            RunQueryTool.TheName => (new RunQueryTool(), new() { ["name"] = "RecentPosts" }),
            RunSqlQueryTool.TheName => (new RunSqlQueryTool(), new() { ["query"] = "SELECT * FROM ContentItemIndex LIMIT 5" }),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "The tool has no test arguments."),
        };
    }
}
