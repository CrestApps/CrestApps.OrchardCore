using System.ComponentModel.DataAnnotations;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Instances.DataSources;
using CrestApps.Core.Infrastructure;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.DataSources;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.Tests.Agent.AIConfiguration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Agent.AIKnowledge;

/// <summary>
/// The save and delete data source tools check the caller's permission, never take a key, password or connection
/// string, apply the data source editors' checks, keep a locked data source's mapping, and refuse to change the data
/// source of the assistant running the turn.
/// </summary>
public sealed class AIDataSourceToolTests
{
    private const string Canary = "CANARY-SECRET-a41e";

    private readonly FakeSourceCatalogManager<AIDataSource> _dataSources = new();
    private readonly InMemoryProfileManager _profiles = new();
    private readonly List<AIToolInstance> _instances = [];

    public static TheoryData<string> ToolNames => new()
    {
        SaveAIDataSourceTool.TheName,
        DeleteAIDataSourceTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Arrange
        var services = BuildServices(signedIn: false);
        AddLockedDataSource();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, new() { ["id"] = "ds-1", ["displayText"] = "Renamed" });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(_dataSources.Updated);
        Assert.Empty(_dataSources.Deleted);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutManageAIDataSources(string toolName)
    {
        // Arrange
        var services = BuildServices(authorize: (permission, _) => permission.Name != AIPermissions.ManageAIDataSources.Name);
        AddLockedDataSource();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, new() { ["id"] = "ds-1", ["displayText"] = "Renamed" });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(_dataSources.Updated);
        Assert.Empty(_dataSources.Deleted);
    }

    [Fact]
    public async Task Save_CreatesASearchIndexDataSourceAndSaysASynchronizationWasQueued()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIDataSourceTool(), services, new()
        {
            ["source"] = AIDataSourceSourceTypes.SearchIndexProfile,
            ["displayText"] = "Product Docs",
            ["sourceIndexProfileName"] = "Articles",
            ["aiKnowledgeBaseIndexProfileName"] = "KnowledgeBase",
            ["titleFieldName"] = "Content.ContentItem.DisplayText",
            ["contentFieldName"] = "Content.BodyAspect.Body",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        Assert.Equal("created", json.GetProperty("saved").GetString());
        Assert.Contains("was queued", json.GetProperty("synchronization").GetString());

        var created = Assert.Single(_dataSources.Created);
        Assert.Equal("Articles", created.SourceIndexProfileName);
        Assert.Equal("KnowledgeBase", created.AIKnowledgeBaseIndexProfileName);
        Assert.Equal("Content.BodyAspect.Body", created.ContentFieldName);
    }

    [Fact]
    public async Task Save_RefusesAnIndexThatIsNotAKnowledgeBaseIndex()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIDataSourceTool(), services, new()
        {
            ["source"] = AIDataSourceSourceTypes.SearchIndexProfile,
            ["displayText"] = "Product Docs",
            ["sourceIndexProfileName"] = "Articles",
            ["aiKnowledgeBaseIndexProfileName"] = "Articles",
            ["contentFieldName"] = "Content.BodyAspect.Body",
        });

        // Assert
        Assert.Contains("is not an AI knowledge base index", result);
        Assert.Empty(_dataSources.Created);
    }

    [Theory]
    [InlineData("apiKey")]
    [InlineData("ApiKey")]
    [InlineData("password")]
    [InlineData("connectionString")]
    [InlineData("base64ApiKey")]
    public async Task Save_RefusesEverySecret(string argument)
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIDataSourceTool(), services, new()
        {
            ["source"] = AIDataSourceSourceTypes.AzureAISearch,
            ["displayText"] = "Search",
            [argument] = Canary,
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result);
        Assert.Contains("tools never take secrets", result);
        Assert.DoesNotContain(Canary, result);
        Assert.Empty(_dataSources.Created);
    }

    [Fact]
    public async Task Save_CreatesAnAzureDataSourceOnlyOnADataConnection()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIDataSourceTool(), services, new()
        {
            ["source"] = AIDataSourceSourceTypes.AzureAISearch,
            ["displayText"] = "Search",
            ["aiKnowledgeBaseIndexProfileName"] = "KnowledgeBase",
            ["contentFieldName"] = "content",
            ["indexName"] = "docs",
        });

        // Assert
        Assert.Contains("is created here on a data connection", result);
        Assert.Empty(_dataSources.Created);
    }

    [Fact]
    public async Task Save_RefusesADataConnectionToACallerWhoMayNotManageThem()
    {
        // Arrange
        var services = BuildServices(authorize: (permission, _) => permission.Name != DataConnectorPermissions.ManageDataConnections.Name);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIDataSourceTool(), services, new()
        {
            ["source"] = AIDataSourceSourceTypes.AzureAISearch,
            ["displayText"] = "Search",
            ["aiKnowledgeBaseIndexProfileName"] = "KnowledgeBase",
            ["contentFieldName"] = "content",
            ["dataConnectionId"] = "conn-1",
            ["indexName"] = "docs",
        });

        // Assert
        Assert.Contains("you may not manage data connections", result);
        Assert.Empty(_dataSources.Created);
    }

    [Fact]
    public async Task Save_CreatesAnAzureDataSourceOnADataConnection()
    {
        // Arrange
        var services = BuildServices(withDataConnections: true);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIDataSourceTool(), services, new()
        {
            ["source"] = AIDataSourceSourceTypes.AzureAISearch,
            ["displayText"] = "Search",
            ["aiKnowledgeBaseIndexProfileName"] = "KnowledgeBase",
            ["contentFieldName"] = "content",
            ["dataConnectionId"] = "conn-1",
            ["indexName"] = "docs",
        });

        // Assert
        Assert.Equal("created", AgentToolTestContext.ReadJson(result).GetProperty("saved").GetString());

        var created = Assert.Single(_dataSources.Created);
        Assert.True(created.TryGet<DataConnectionReference>(out var reference));
        Assert.Equal("conn-1", reference.ConnectionId);
        Assert.True(created.TryGet<AzureAISearchSourceMetadata>(out var metadata));
        Assert.Equal("docs", metadata.IndexName);
        Assert.Null(metadata.ApiKey);
    }

    [Fact]
    public async Task Save_RenamesALockedDataSource()
    {
        // Arrange
        var services = BuildServices();
        var dataSource = AddLockedDataSource();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIDataSourceTool(), services, new() { ["id"] = dataSource.ItemId, ["displayText"] = "Renamed" });

        // Assert
        Assert.Equal("updated", AgentToolTestContext.ReadJson(result).GetProperty("saved").GetString());
        Assert.Same(dataSource, Assert.Single(_dataSources.Updated));
        Assert.Equal("Renamed", dataSource.DisplayText);
    }

    [Fact]
    public async Task Save_KeepsTheMappingOfALockedDataSource()
    {
        // Arrange
        var services = BuildServices();
        var dataSource = AddLockedDataSource();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIDataSourceTool(), services, new() { ["id"] = dataSource.ItemId, ["contentFieldName"] = "other" });

        // Assert
        Assert.Contains("is locked", result);
        Assert.Empty(_dataSources.Updated);
        Assert.Equal("content", dataSource.ContentFieldName);
    }

    [Fact]
    public async Task Save_RelaysTheCatalogValidation()
    {
        // Arrange
        var services = BuildServices();
        _dataSources.Validate = _ =>
        {
            var validation = new ValidationResultDetails();
            validation.Fail(new ValidationResult("Display text is required."));

            return validation;
        };

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIDataSourceTool(), services, new()
        {
            ["source"] = AIDataSourceSourceTypes.SearchIndexProfile,
            ["displayText"] = "Product Docs",
            ["sourceIndexProfileName"] = "Articles",
            ["aiKnowledgeBaseIndexProfileName"] = "KnowledgeBase",
            ["contentFieldName"] = "body",
        });

        // Assert
        Assert.Contains("Display text is required.", result);
        Assert.Empty(_dataSources.Created);
    }

    [Fact]
    public async Task Save_RefusesTheDataSourceOfTheRunningAssistant()
    {
        // Arrange
        var services = BuildServices();
        var dataSource = AddLockedDataSource();
        var running = AddProfileUsing(dataSource.ItemId);

        using var turn = AIConfigurationToolHarness.BeginTurn(running);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIDataSourceTool(), services, new() { ["id"] = dataSource.ItemId, ["displayText"] = "Renamed" });

        // Assert
        Assert.StartsWith("Refused:", result);
        Assert.Empty(_dataSources.Updated);
        Assert.Equal("Docs", dataSource.DisplayText);
    }

    [Fact]
    public async Task Delete_RefusesADataSourceTheRunningAssistantSearchesThroughAToolInstance()
    {
        // Arrange
        var services = BuildServices();
        var dataSource = AddLockedDataSource();
        var instance = new AIToolInstance { ItemId = "instance-1", Name = "docs-search", Source = DataSourceSearchToolConstants.SourceName };
        instance.Put(new DataSourceSearchToolSettings { DataSourceId = dataSource.ItemId });
        _instances.Add(instance);

        var running = new AIProfile { ItemId = "running", Name = "running", DisplayText = "Running" };
        running.Put(new AIToolInstanceMetadata { ToolInstanceNames = ["docs-search"] });
        _profiles.Add(running);

        using var turn = AIConfigurationToolHarness.BeginTurn(running);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteAIDataSourceTool(), services, new() { ["id"] = dataSource.ItemId });

        // Assert
        Assert.StartsWith("Refused:", result);
        Assert.Empty(_dataSources.Deleted);
    }

    [Fact]
    public async Task Delete_DeletesTheDataSourceAndNamesTheProfilesThatUsedIt()
    {
        // Arrange
        var services = BuildServices();
        var dataSource = AddLockedDataSource();
        AddProfileUsing(dataSource.ItemId);

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DeleteAIDataSourceTool(), services, new() { ["id"] = dataSource.ItemId }));

        // Assert
        Assert.Equal(dataSource.ItemId, json.GetProperty("deleted").GetProperty("id").GetString());
        Assert.Equal("Support Assistant", Assert.Single(json.GetProperty("profilesThatUsedIt").EnumerateArray()).GetString());
        Assert.Contains("was queued", json.GetProperty("cleanup").GetString());
        Assert.Same(dataSource, Assert.Single(_dataSources.Deleted));
    }

    [Fact]
    public async Task Delete_SaysSoWhenNoDataSourceHasTheId()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteAIDataSourceTool(), services, new() { ["id"] = "missing" });

        // Assert
        Assert.Contains("no data source has the ID 'missing'", result);
        Assert.Empty(_dataSources.Deleted);
    }

    private AIDataSource AddLockedDataSource()
        => _dataSources.Add(new AIDataSource
        {
            ItemId = "ds-1",
            DisplayText = "Docs",
            Source = AIDataSourceSourceTypes.SearchIndexProfile,
            SourceIndexProfileName = "Articles",
            AIKnowledgeBaseIndexProfileName = "KnowledgeBase",
            ContentFieldName = "content",
        });

    private AIProfile AddProfileUsing(string dataSourceId)
    {
        var profile = new AIProfile { ItemId = "support", Name = "support", DisplayText = "Support Assistant" };
        profile.Put(new DataSourceMetadata { DataSourceId = dataSourceId });
        _profiles.Add(profile);

        return profile;
    }

    private ServiceProvider BuildServices(bool signedIn = true, Func<Permission, object, bool> authorize = null, bool withDataConnections = false)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);
        var indexStore = new Mock<IIndexProfileStore>();
        indexStore.Setup(store => store.FindByNameAsync("KnowledgeBase"))
            .ReturnsAsync(new IndexProfile { Name = "KnowledgeBase", ProviderName = "Elasticsearch", Type = DataSourceConstants.IndexingTaskType });
        indexStore.Setup(store => store.FindByNameAsync("Articles"))
            .ReturnsAsync(new IndexProfile { Name = "Articles", ProviderName = "Elasticsearch", Type = "Content" });

        var toolInstances = new Mock<INamedCatalog<AIToolInstance>>();
        toolInstances.Setup(catalog => catalog.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string name, CancellationToken _) => ValueTask.FromResult(_instances.FirstOrDefault(instance => string.Equals(instance.Name, name, StringComparison.OrdinalIgnoreCase))));

        services.AddSingleton<ISourceCatalogManager<AIDataSource>>(_dataSources);
        services.AddSingleton<IAIProfileManager>(_profiles);
        services.AddSingleton(indexStore.Object);
        services.AddSingleton(toolInstances.Object);
        services.Configure<AIDataSourceSourceOptions>(options =>
        {
            options.AddOrUpdate(AIDataSourceSourceTypes.SearchIndexProfile, new LocalizedString("Search index", "Search index"), new LocalizedString("Search index", "Reads a search index."));
            options.AddOrUpdate(AIDataSourceSourceTypes.AzureAISearch, new LocalizedString("Azure AI Search", "Azure AI Search"), new LocalizedString("Azure AI Search", "Reads an Azure AI Search index."));
        });

        if (withDataConnections)
        {
            services.AddSingleton(new Mock<IDataConnectionResolver>().Object);
        }

        return services.BuildServiceProvider();
    }

    private static AIFunction CreateTool(string toolName)
        => toolName == SaveAIDataSourceTool.TheName
            ? new SaveAIDataSourceTool()
            : new DeleteAIDataSourceTool();
}
