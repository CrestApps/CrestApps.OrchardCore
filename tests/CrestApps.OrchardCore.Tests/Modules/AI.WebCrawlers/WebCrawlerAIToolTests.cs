using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.AI.WebCrawlers.Strategies;
using CrestApps.Core.AI.WebCrawlers.Strategies.Sitemap;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.WebCrawlers;
using CrestApps.OrchardCore.AI.WebCrawlers.AI;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIKnowledge;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.AI.WebCrawlers;

/// <summary>
/// The web crawler tools check the caller's permission, apply the crawler editor's checks, report the synchronization
/// a save queues, and run a crawler through the re-index planner.
/// </summary>
public sealed class WebCrawlerAIToolTests
{
    private readonly FakeSourceCatalogManager<WebCrawler> _crawlers = new();
    private readonly FakeSourceCatalogManager<AIDataSource> _dataSources = new();
    private readonly Mock<IWebCrawlerReindexPlanner> _planner = new();

    public WebCrawlerAIToolTests()
    {
        _dataSources.Add(new AIDataSource { ItemId = "web-ds", DisplayText = "Docs", Source = AIDataSourceSourceTypes.Web });
    }

    public static TheoryData<string> ToolNames => new()
    {
        ListAIWebCrawlersTool.TheName,
        SaveAIWebCrawlerTool.TheName,
        DeleteAIWebCrawlerTool.TheName,
        RunAIWebCrawlerTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Arrange
        var services = BuildServices(signedIn: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, new() { ["id"] = "crawler-1" });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutManageWebCrawlers(string toolName)
    {
        // Arrange
        var services = BuildServices(authorize: (permission, _) => permission.Name != WebCrawlerPermissions.ManageWebCrawlers.Name);
        AddCrawler();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, new() { ["id"] = "crawler-1" });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(_crawlers.Created);
        Assert.Empty(_crawlers.Updated);
        Assert.Empty(_crawlers.Deleted);
    }

    [Fact]
    public async Task List_ReturnsTheCrawlersWithTheirSitemapSettingsAndTheStrategies()
    {
        // Arrange
        var services = BuildServices();
        AddCrawler();

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListAIWebCrawlersTool(), services));

        // Assert
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal("crawler-1", item.GetProperty("id").GetString());
        Assert.Equal("https://docs.example.com", item.GetProperty("sitemap").GetProperty("baseUrl").GetString());
        Assert.Equal("Docs", item.GetProperty("dataSource").GetProperty("displayText").GetString());
        Assert.Equal(1, json.GetProperty("total").GetInt32());
        Assert.Equal(WebCrawlerConstants.Strategies.Sitemap, Assert.Single(json.GetProperty("strategies").EnumerateArray()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task List_SaysSoWhenNoCrawlerHasTheId()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIWebCrawlersTool(), services, new() { ["id"] = "missing" });

        // Assert
        Assert.Contains("No web crawler has the ID 'missing'", result);
    }

    [Fact]
    public async Task Save_CreatesACrawlerAndSaysASynchronizationWasQueued()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIWebCrawlerTool(), services, new()
        {
            ["displayText"] = "Docs site",
            ["aiDataSourceId"] = "web-ds",
            ["reindexIntervalMinutes"] = 1440,
            ["sitemap"] = JsonSerializer.SerializeToElement(new
            {
                baseUrl = "https://docs.example.com",
                maxPages = 500,
                includeUrlPatterns = new[] { "^https://docs\\.example\\.com/guide/" },
            }),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        Assert.Equal("created", json.GetProperty("saved").GetString());
        Assert.Contains("was queued", json.GetProperty("synchronization").GetString());
        Assert.Contains("has not run yet", json.GetProperty("synchronization").GetString());

        var created = Assert.Single(_crawlers.Created);
        Assert.Equal(WebCrawlerConstants.Strategies.Sitemap, created.Source);
        Assert.Equal(1440, created.ReindexIntervalMinutes);
        Assert.True(created.TryGet<SitemapWebCrawlerMetadata>(out var metadata));
        Assert.Equal(500, metadata.MaxPages);
        Assert.Equal("^https://docs\\.example\\.com/guide/", Assert.Single(metadata.IncludeUrlPatterns));
    }

    [Fact]
    public async Task Save_UpdatesOnlyTheGivenSitemapSettings()
    {
        // Arrange
        var services = BuildServices();
        var crawler = AddCrawler();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIWebCrawlerTool(), services, new()
        {
            ["id"] = crawler.ItemId,
            ["sitemap"] = JsonSerializer.SerializeToElement(new { maxPages = 50 }),
        });

        // Assert
        Assert.Equal("updated", AgentToolTestContext.ReadJson(result).GetProperty("saved").GetString());
        Assert.Same(crawler, Assert.Single(_crawlers.Updated));
        Assert.True(crawler.TryGet<SitemapWebCrawlerMetadata>(out var metadata));
        Assert.Equal(50, metadata.MaxPages);
        Assert.Equal("https://docs.example.com", metadata.BaseUrl);
    }

    [Theory]
    [InlineData("docs.example.com", "absolute URL")]
    [InlineData("", "Provide a base URL or an explicit sitemap URL.")]
    public async Task Save_RefusesAnAddressTheEditorRefuses(string baseUrl, string expected)
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIWebCrawlerTool(), services, new()
        {
            ["displayText"] = "Docs site",
            ["aiDataSourceId"] = "web-ds",
            ["sitemap"] = JsonSerializer.SerializeToElement(new { baseUrl }),
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result);
        Assert.Contains(expected, result);
        Assert.Empty(_crawlers.Created);
    }

    [Fact]
    public async Task Save_RefusesAnInvalidUrlPattern()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIWebCrawlerTool(), services, new()
        {
            ["displayText"] = "Docs site",
            ["aiDataSourceId"] = "web-ds",
            ["sitemap"] = JsonSerializer.SerializeToElement(new { baseUrl = "https://docs.example.com", excludeUrlPatterns = new[] { "([" } }),
        });

        // Assert
        Assert.Contains("is not a valid regular expression", result);
        Assert.Empty(_crawlers.Created);
    }

    [Fact]
    public async Task Save_RelaysTheCatalogValidation()
    {
        // Arrange
        var services = BuildServices();
        _crawlers.Validate = _ =>
        {
            var validation = new ValidationResultDetails();
            validation.Fail(new ValidationResult("A crawl strategy can only feed a Web or an Ingested data source."));

            return validation;
        };

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIWebCrawlerTool(), services, new()
        {
            ["displayText"] = "Docs site",
            ["aiDataSourceId"] = "web-ds",
            ["sitemap"] = JsonSerializer.SerializeToElement(new { baseUrl = "https://docs.example.com" }),
        });

        // Assert
        Assert.Contains("can only feed a Web or an Ingested data source", result);
        Assert.Empty(_crawlers.Created);
    }

    [Fact]
    public async Task Delete_DeletesTheCrawlerAndSaysTheCleanupWasQueued()
    {
        // Arrange
        var services = BuildServices();
        var crawler = AddCrawler();

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DeleteAIWebCrawlerTool(), services, new() { ["id"] = crawler.ItemId }));

        // Assert
        Assert.Equal(crawler.ItemId, json.GetProperty("deleted").GetProperty("id").GetString());
        Assert.Contains("was queued", json.GetProperty("synchronization").GetString());
        Assert.Same(crawler, Assert.Single(_crawlers.Deleted));
    }

    [Fact]
    public async Task Delete_SaysSoWhenNoCrawlerHasTheId()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteAIWebCrawlerTool(), services, new() { ["id"] = "missing" });

        // Assert
        Assert.Contains("no web crawler has the ID 'missing'", result);
        Assert.Empty(_crawlers.Deleted);
    }

    [Fact]
    public async Task Run_ReturnsTheCountsAndSaysTheIndexingWasQueued()
    {
        // Arrange
        var services = BuildServices();
        var crawler = AddCrawler();
        _planner.Setup(planner => planner.PlanAndEnqueueAsync(crawler, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebCrawlerReindexResult(3, 2, 1, 10) { DiscoveredCount = 16 });

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new RunAIWebCrawlerTool(), services, new() { ["id"] = crawler.ItemId }));

        // Assert
        Assert.Equal(3, json.GetProperty("newPages").GetInt32());
        Assert.Equal(2, json.GetProperty("changedPages").GetInt32());
        Assert.Equal(1, json.GetProperty("removedPages").GetInt32());
        Assert.Contains("queued for indexing", json.GetProperty("indexing").GetString());
    }

    [Fact]
    public async Task Run_RelaysAFailedDiscovery()
    {
        // Arrange
        var services = BuildServices();
        var crawler = AddCrawler();
        _planner.Setup(planner => planner.PlanAndEnqueueAsync(crawler, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebCrawlerReindexResult.Failed("The sitemap could not be read."));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunAIWebCrawlerTool(), services, new() { ["id"] = crawler.ItemId });

        // Assert
        Assert.Contains("The sitemap could not be read.", result);
        Assert.Contains("Nothing was queued", result);
    }

    private WebCrawler AddCrawler()
    {
        var crawler = new WebCrawler
        {
            ItemId = "crawler-1",
            DisplayText = "Docs site",
            Source = WebCrawlerConstants.Strategies.Sitemap,
            AIDataSourceId = "web-ds",
        };

        crawler.Put(new SitemapWebCrawlerMetadata
        {
            BaseUrl = "https://docs.example.com",
            MaxPages = 200,
        });

        return _crawlers.Add(crawler);
    }

    private ServiceProvider BuildServices(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton<ISourceCatalogManager<WebCrawler>>(_crawlers);
        services.AddSingleton<ISourceCatalogManager<AIDataSource>>(_dataSources);
        services.AddSingleton(_planner.Object);
        services.Configure<WebCrawlerStrategyOptions>(options => options.AddOrUpdate(
            WebCrawlerConstants.Strategies.Sitemap,
            new LocalizedString("Sitemap", "Sitemap"),
            new LocalizedString("Sitemap", "Reads the sitemap.")));

        return services.BuildServiceProvider();
    }

    private static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            ListAIWebCrawlersTool.TheName => new ListAIWebCrawlersTool(),
            SaveAIWebCrawlerTool.TheName => new SaveAIWebCrawlerTool(),
            DeleteAIWebCrawlerTool.TheName => new DeleteAIWebCrawlerTool(),
            _ => new RunAIWebCrawlerTool(),
        };
}
