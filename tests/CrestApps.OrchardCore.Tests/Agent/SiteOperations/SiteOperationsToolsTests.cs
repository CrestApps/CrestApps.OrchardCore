using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.BackgroundTasks;
using CrestApps.OrchardCore.AI.Agent.DataLocalization;
using CrestApps.OrchardCore.AI.Agent.HealthChecks;
using CrestApps.OrchardCore.AI.Agent.UrlRewriting;
using OrchardCore.Entities;
using OrchardCore.UrlRewriting.Models;

namespace CrestApps.OrchardCore.Tests.Agent.SiteOperations;

/// <summary>
/// The site operations tools: background tasks are listed with their state and run on demand through the background
/// service monitor; URL rewriting rules are listed, saved and deleted through the rule manager, which validates them;
/// the health checks run through the health check service; dynamic translations are read per culture and saved through
/// the Translations recipe step.
/// </summary>
public sealed class SiteOperationsToolsTests
{
    private static readonly string _indexingTask = typeof(SiteOperationsToolHarness.IndexingTestBackgroundTask).FullName;
    private static readonly string _cleanupTask = typeof(SiteOperationsToolHarness.CleanupTestBackgroundTask).FullName;

    [Fact]
    public async Task ListBackgroundTasks_ReturnsEachTaskWithItsSettingsAndState()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListBackgroundTasksTool(), services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();
        var cleanup = items.Single(item => item.GetProperty("name").GetString() == _cleanupTask);
        var indexing = items.Single(item => item.GetProperty("name").GetString() == _indexingTask);

        Assert.Equal(2, json.GetProperty("total").GetInt32());
        Assert.False(cleanup.GetProperty("enabled").GetBoolean());
        Assert.Equal("0 2 * * *", cleanup.GetProperty("schedule").GetString());
        Assert.Equal("NotLoaded", cleanup.GetProperty("status").GetString());
        Assert.True(indexing.GetProperty("enabled").GetBoolean());
        Assert.Equal("Index Update", indexing.GetProperty("title").GetString());
        Assert.Equal("Idle", indexing.GetProperty("status").GetString());
        Assert.Equal(12, indexing.GetProperty("runCount").GetInt64());
        Assert.Equal("2026-11-17T10:00:00+00:00", indexing.GetProperty("nextStartTime").GetString());
    }

    [Fact]
    public async Task ListBackgroundTasks_WithTheFailedStatus_KeepsOnlyTheTasksWhoseLastRunFailed()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListBackgroundTasksTool(), services, new() { ["status"] = "failed" });

        // Assert
        var item = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray());

        Assert.Equal(_indexingTask, item.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ListBackgroundTasks_WithAName_ReturnsTheRecentRunsWithoutTheStackTrace()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListBackgroundTasksTool(), services, new() { ["name"] = _indexingTask });

        // Assert
        var task = AgentToolTestContext.ReadJson(result).GetProperty("task");
        var run = Assert.Single(task.GetProperty("recentRuns").EnumerateArray());

        Assert.Equal("Failed", run.GetProperty("result").GetString());
        Assert.Equal("The index is locked.", run.GetProperty("errorMessage").GetString());
        Assert.Equal("*/5 * * * *", task.GetProperty("defaultSchedule").GetString());
        Assert.DoesNotContain("Somewhere", result);
    }

    [Fact]
    public async Task ListBackgroundTasks_WithAnUnknownName_SaysSo()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListBackgroundTasksTool(), services, new() { ["name"] = "Nope" });

        // Assert
        Assert.Contains("There is no background task named 'Nope'", result);
    }

    [Fact]
    public async Task RunBackgroundTask_RequestsTheRunAndSaysItIsQueued()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunBackgroundTaskTool(), services, new() { ["names"] = new[] { _indexingTask } });

        // Assert
        var requested = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("requested").EnumerateArray());

        Assert.Equal([_indexingTask], harness.RunRequests);
        Assert.Equal("Queued", requested.GetProperty("result").GetString());
        Assert.Contains("has not finished", result);
    }

    [Fact]
    public async Task RunBackgroundTask_WithAnUnknownName_RunsNothing()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunBackgroundTaskTool(), services, new() { ["names"] = new[] { _indexingTask, "Nope" } });

        // Assert
        Assert.StartsWith("Nothing was run", result);
        Assert.Empty(harness.RunRequests);
    }

    [Fact]
    public async Task RunBackgroundTask_WhenTheBackgroundServiceIsNotRegistered_RunsNothing()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness
        {
            HasMonitor = false,
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunBackgroundTaskTool(), services, new() { ["names"] = new[] { _indexingTask } });

        // Assert
        Assert.Contains("background service is not registered", result);
    }

    [Fact]
    public async Task ListUrlRewriteRules_ReturnsTheRulesInOrderWithTheirOptions()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListUrlRewriteRulesTool(), services);

        // Assert
        var rule = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray());

        Assert.Equal(SiteOperationsToolHarness.RedirectRuleId, rule.GetProperty("id").GetString());
        Assert.Equal(1, rule.GetProperty("position").GetInt32());
        Assert.Equal("Redirect", rule.GetProperty("type").GetString());
        Assert.Equal("^old-blog/(.*)$", rule.GetProperty("pattern").GetString());
        Assert.Equal(301, rule.GetProperty("redirectStatusCode").GetInt32());
    }

    [Fact]
    public async Task SaveUrlRewriteRule_CreatesAValidRuleAfterTheOthersAndReloadsTheTenant()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveUrlRewriteRuleTool(), services, new()
        {
            ["type"] = "Rewrite",
            ["name"] = "Docs",
            ["pattern"] = "^docs/(.*)$",
            ["substitutionPattern"] = "/documentation/$1",
            ["skipFurtherRules"] = true,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var rule = json.GetProperty("rule");
        var saved = harness.Rules[rule.GetProperty("id").GetString()];

        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal(2, rule.GetProperty("position").GetInt32());
        Assert.Equal("Rewrite", saved.Source);
        Assert.Equal("Docs", saved.Name);
        Assert.True(GetMetadata<UrlRewriteSourceMetadata>(saved).SkipFurtherRules);
        Assert.Equal(1, harness.Releases);
    }

    [Fact]
    public async Task SaveUrlRewriteRule_ChangesOnlyTheGivenFields()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        await AgentToolTestContext.InvokeAsync(new SaveUrlRewriteRuleTool(), services, new()
        {
            ["id"] = SiteOperationsToolHarness.RedirectRuleId,
            ["redirectType"] = "Found",
        });

        // Assert
        var metadata = GetMetadata<UrlRedirectSourceMetadata>(harness.Rules[SiteOperationsToolHarness.RedirectRuleId]);

        Assert.Equal(RedirectType.Found, metadata.RedirectType);
        Assert.Equal("^old-blog/(.*)$", metadata.Pattern);
        Assert.True(metadata.IsCaseInsensitive);
        Assert.Equal(1, harness.Releases);
    }

    [Fact]
    public async Task SaveUrlRewriteRule_WithAnInvalidPattern_SavesNothingAndLeavesTheStoredRuleAlone()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();
        var stored = harness.Rules[SiteOperationsToolHarness.RedirectRuleId];

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveUrlRewriteRuleTool(), services, new()
        {
            ["id"] = SiteOperationsToolHarness.RedirectRuleId,
            ["pattern"] = "^old-blog/(.*$",
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result);
        Assert.Contains("valid Match URL Pattern", result);
        Assert.Same(stored, harness.Rules[SiteOperationsToolHarness.RedirectRuleId]);
        Assert.Equal("^old-blog/(.*)$", GetMetadata<UrlRedirectSourceMetadata>(stored).Pattern);
        Assert.Equal(0, harness.Releases);
    }

    [Fact]
    public async Task SaveUrlRewriteRule_WithAnUnknownId_SavesNothing()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveUrlRewriteRuleTool(), services, new()
        {
            ["id"] = "nope",
            ["name"] = "Renamed",
        });

        // Assert
        Assert.Contains("there is no URL rewriting rule with the ID 'nope'", result);
        Assert.Single(harness.Rules);
        Assert.Equal(0, harness.Releases);
    }

    [Fact]
    public async Task DeleteUrlRewriteRule_DeletesTheRuleAndReloadsTheTenant()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteUrlRewriteRuleTool(), services, new() { ["ids"] = new[] { SiteOperationsToolHarness.RedirectRuleId } });

        // Assert
        var deleted = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("deleted").EnumerateArray());

        Assert.Equal("Old blog", deleted.GetProperty("name").GetString());
        Assert.Empty(harness.Rules);
        Assert.Equal(1, harness.Releases);
    }

    [Fact]
    public async Task DeleteUrlRewriteRule_WithAnUnknownId_DeletesNothing()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteUrlRewriteRuleTool(), services, new() { ["ids"] = new[] { SiteOperationsToolHarness.RedirectRuleId, "nope" } });

        // Assert
        Assert.StartsWith("Nothing was deleted", result);
        Assert.Single(harness.Rules);
        Assert.Equal(0, harness.Releases);
    }

    [Fact]
    public async Task GetSiteHealth_ReturnsEachCheckWithoutTheException()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetSiteHealthTool(), services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var checks = json.GetProperty("checks").EnumerateArray().ToList();

        Assert.Equal("Unhealthy", json.GetProperty("status").GetString());
        Assert.Equal(["Database", "Redis"], checks.Select(check => check.GetProperty("name").GetString()));
        Assert.Equal("Couldn't connect to the Redis server.", checks[1].GetProperty("description").GetString());
        Assert.DoesNotContain("secret", result);
    }

    [Fact]
    public async Task GetSiteHealth_WithAnUnknownCheck_SaysSo()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetSiteHealthTool(), services, new() { ["checkName"] = "Nope" });

        // Assert
        Assert.Contains("There is no health check named 'Nope'", result);
    }

    [Fact]
    public async Task ListDynamicTranslations_ReturnsTheStringsOfTheCultureAndTheProgressOfEach()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListDynamicTranslationsTool(), services, new() { ["culture"] = "fr-fr" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();
        var blogPost = items.Single(item => item.GetProperty("key").GetString() == "Blog Post");
        var french = json.GetProperty("progress").EnumerateArray().Single(culture => culture.GetProperty("culture").GetString() == "fr-FR");

        Assert.Equal("fr-FR", json.GetProperty("culture").GetString());
        Assert.Equal(3, items.Count);
        Assert.Equal("Article de blog", blogPost.GetProperty("translation").GetString());
        Assert.Equal(1, french.GetProperty("translated").GetInt32());
        Assert.Equal(3, french.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task ListDynamicTranslations_UntranslatedOnly_LeavesOutTheTranslatedStrings()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListDynamicTranslationsTool(), services, new()
        {
            ["culture"] = "fr-FR",
            ["untranslatedOnly"] = true,
        });

        // Assert
        var keys = AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("key").GetString());

        Assert.Equal(["Article", "Manage blogs"], keys);
    }

    [Fact]
    public async Task ListDynamicTranslations_WithAnUnsupportedCulture_SaysWhichAreSupported()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListDynamicTranslationsTool(), services, new() { ["culture"] = "de-DE" });

        // Assert
        Assert.Contains("is not one of the site's supported cultures (en-US, fr-FR)", result);
    }

    [Fact]
    public async Task SaveDynamicTranslations_RunsTheTranslationsStepUnderTheSiteCultureName()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveDynamicTranslationsTool(), services, new()
        {
            ["culture"] = "fr-fr",
            ["translations"] = JsonNode.Parse("""[ { "context": "Content Types", "key": "Article", "value": "Article de presse" } ]"""),
        });

        // Assert
        var step = Assert.Single(harness.RecipeSteps);
        var translation = step.Step["translations"][0];

        Assert.Equal("fr-FR", AgentToolTestContext.ReadJson(result).GetProperty("culture").GetString());
        Assert.Equal("Translations", step.Name);
        Assert.Equal("fr-FR", translation["culture"].GetValue<string>());
        Assert.Equal("Content Types", translation["context"].GetValue<string>());
        Assert.Equal("Article", translation["key"].GetValue<string>());
        Assert.Equal("Article de presse", translation["value"].GetValue<string>());
    }

    [Theory]
    [InlineData("""[ { "context": "Content Types", "key": "Nope", "value": "Non" } ]""", "is not a translatable string")]
    [InlineData("""[ { "context": "Content Types", "key": "Article", "value": " " } ]""", "is empty")]
    public async Task SaveDynamicTranslations_WithAnUnknownStringOrAnEmptyValue_SavesNothing(string translations, string expected)
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveDynamicTranslationsTool(), services, new()
        {
            ["culture"] = "fr-FR",
            ["translations"] = JsonNode.Parse(translations),
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result);
        Assert.Contains(expected, result);
        Assert.Empty(harness.RecipeSteps);
    }

    [Fact]
    public async Task SaveUrlRewriteRule_TwiceInOneScope_KeepsBothChanges()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness
        {
            CachedReads = true,
        };

        using var services = harness.Build();

        // Act
        await AgentToolTestContext.InvokeAsync(new SaveUrlRewriteRuleTool(), services, new()
        {
            ["id"] = SiteOperationsToolHarness.RedirectRuleId,
            ["redirectType"] = "Found",
        });

        await AgentToolTestContext.InvokeAsync(new SaveUrlRewriteRuleTool(), services, new()
        {
            ["id"] = SiteOperationsToolHarness.RedirectRuleId,
            ["name"] = "Old blog posts",
        });

        // Assert
        var rule = harness.Rules[SiteOperationsToolHarness.RedirectRuleId];

        Assert.Equal("Old blog posts", rule.Name);
        Assert.Equal(RedirectType.Found, GetMetadata<UrlRedirectSourceMetadata>(rule).RedirectType);
    }

    [Fact]
    public async Task DeleteUrlRewriteRule_AfterCreatingItInTheSameScope_DeletesIt()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness
        {
            CachedReads = true,
        };

        using var services = harness.Build();

        var created = await AgentToolTestContext.InvokeAsync(new SaveUrlRewriteRuleTool(), services, new()
        {
            ["type"] = "Redirect",
            ["name"] = "Sale",
            ["pattern"] = "^sale$",
            ["substitutionPattern"] = "/offers",
        });

        var id = AgentToolTestContext.ReadJson(created).GetProperty("rule").GetProperty("id").GetString();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteUrlRewriteRuleTool(), services, new() { ["ids"] = new[] { id } });

        // Assert
        Assert.DoesNotContain("Nothing was deleted", result);
        Assert.False(harness.Rules.ContainsKey(id));
    }

    [Fact]
    public async Task SaveDynamicTranslations_TwiceInOneScope_SendsTheFirstTranslationsAgain()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        // Act
        await AgentToolTestContext.InvokeAsync(new SaveDynamicTranslationsTool(), services, new()
        {
            ["culture"] = "fr-FR",
            ["translations"] = JsonNode.Parse("""[ { "context": "Content Types", "key": "Article", "value": "Article de presse" } ]"""),
        });

        await AgentToolTestContext.InvokeAsync(new SaveDynamicTranslationsTool(), services, new()
        {
            ["culture"] = "fr-FR",
            ["translations"] = JsonNode.Parse("""[ { "context": "Permissions", "key": "Manage blogs", "value": "Gérer les blogs" } ]"""),
        });

        // Assert
        var keys = harness.RecipeSteps[1].Step["translations"].AsArray().Select(translation => translation["key"].GetValue<string>());

        Assert.Equal(2, harness.RecipeSteps.Count);
        Assert.Equal(["Article", "Manage blogs"], keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ListDynamicTranslations_AfterASaveInTheSameScope_AsksToListInTheNextReply()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness();
        using var services = harness.Build();

        await AgentToolTestContext.InvokeAsync(new SaveDynamicTranslationsTool(), services, new()
        {
            ["culture"] = "fr-FR",
            ["translations"] = JsonNode.Parse("""[ { "context": "Content Types", "key": "Article", "value": "Article de presse" } ]"""),
        });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListDynamicTranslationsTool(), services, new() { ["culture"] = "fr-FR" });

        // Assert
        Assert.StartsWith("Translations were saved earlier in this reply", result);
    }

    private static T GetMetadata<T>(RewriteRule rule)
        where T : new()
    {
        Assert.True(rule.TryGet<T>(out var metadata));

        return metadata;
    }
}
