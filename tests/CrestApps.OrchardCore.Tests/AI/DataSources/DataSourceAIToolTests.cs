using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Infrastructure;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.AI;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIKnowledge;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.AI.DataSources;

/// <summary>
/// The list, sync and search tools of the Data Sources module check the caller's permission, never return a stored key,
/// password or connection string, and queue a synchronization rather than run one.
/// </summary>
public sealed class DataSourceAIToolTests
{
    private const string Canary = "CANARY-SECRET-51c9";

    private readonly FakeSourceCatalogManager<AIDataSource> _dataSources = new();
    private readonly Mock<IAIDataSourceIndexingQueue> _queue = new();

    public static TheoryData<string> ToolNames => new()
    {
        ListAIDataSourcesTool.TheName,
        SyncAIDataSourceTool.TheName,
        SearchAIDataSourceTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Arrange
        var services = BuildServices(signedIn: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, new() { ["id"] = "azure", ["query"] = "refunds" });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutManageAIDataSources(string toolName)
    {
        // Arrange
        var services = BuildServices(authorize: (permission, _) => permission.Name != AIPermissions.ManageAIDataSources.Name);
        AddDataSources();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, new() { ["id"] = "azure", ["query"] = "refunds" });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        _queue.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task List_NeverReturnsAStoredSecret_OnlyWhetherOneIsStored()
    {
        // Arrange
        var services = BuildServices();
        AddDataSources();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(ListAIDataSourcesTool.TheName), services);

        // Assert
        Assert.DoesNotContain(Canary, result);

        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("id").GetString());
        Assert.True(items["azure"].GetProperty("azureAISearch").GetProperty("hasApiKey").GetBoolean());
        Assert.Equal("https://search.example.net", items["azure"].GetProperty("azureAISearch").GetProperty("endpoint").GetString());

        var elasticsearch = items["elastic"].GetProperty("elasticsearch");
        Assert.True(elasticsearch.GetProperty("hasPassword").GetBoolean());
        Assert.True(elasticsearch.GetProperty("hasApiKey").GetBoolean());
        Assert.True(elasticsearch.GetProperty("hasBase64ApiKey").GetBoolean());
        Assert.Equal("reader", elasticsearch.GetProperty("username").GetString());

        Assert.True(items["postgres"].GetProperty("postgreSQL").GetProperty("hasConnectionString").GetBoolean());
        Assert.Equal("conn-1", items["connected"].GetProperty("dataConnectionId").GetString());
        Assert.Equal("kb", Assert.Single(json.GetProperty("knowledgeBaseIndexes").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal(AIDataSourceSourceTypes.SearchIndexProfile, Assert.Single(json.GetProperty("sourceTypes").EnumerateArray()).GetProperty("sourceType").GetString());
    }

    [Fact]
    public async Task List_SaysSoWhenNoDataSourceHasTheId()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(ListAIDataSourcesTool.TheName), services, new() { ["id"] = "missing" });

        // Assert
        Assert.Contains("No data source has the ID 'missing'", result);
    }

    [Fact]
    public async Task Sync_QueuesTheSynchronizationAndSaysItHasNotFinished()
    {
        // Arrange
        var services = BuildServices();
        AddDataSources();

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(CreateTool(SyncAIDataSourceTool.TheName), services, new() { ["id"] = "azure" }));

        // Assert
        Assert.True(json.GetProperty("queued").GetBoolean());
        Assert.Contains("has not finished", json.GetProperty("message").GetString());
        _queue.Verify(queue => queue.QueueSyncDataSourceAsync(It.Is<AIDataSource>(source => source.ItemId == "azure"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sync_SaysSoWhenNoDataSourceHasTheId()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(SyncAIDataSourceTool.TheName), services, new() { ["id"] = "missing" });

        // Assert
        Assert.Contains("no data source has the ID 'missing'", result);
        _queue.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Search_SaysSoWhenNoDataSourceHasTheId()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(SearchAIDataSourceTool.TheName), services, new() { ["id"] = "missing", ["query"] = "refunds" });

        // Assert
        Assert.Contains("no data source has the ID 'missing'", result);
    }

    [Fact]
    public async Task Search_RefusesATopNOutOfRange()
    {
        // Arrange
        var services = BuildServices();
        AddDataSources();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(SearchAIDataSourceTool.TheName), services, new() { ["id"] = "azure", ["query"] = "refunds", ["topN"] = 500 });

        // Assert
        Assert.Contains("topN must be a whole number from 1 to 20", result);
    }

    private void AddDataSources()
    {
        var azure = new AIDataSource { ItemId = "azure", DisplayText = "Azure", Source = AIDataSourceSourceTypes.AzureAISearch, AIKnowledgeBaseIndexProfileName = "kb", ContentFieldName = "content" };
        azure.Put(new AzureAISearchSourceMetadata { Endpoint = "https://search.example.net", IndexName = "docs", AuthenticationType = AzureAISearchSourceMetadata.ApiKeyAuthenticationType, ApiKey = Canary });

        var elastic = new AIDataSource { ItemId = "elastic", DisplayText = "Elastic", Source = AIDataSourceSourceTypes.Elasticsearch };
        elastic.Put(new ElasticsearchSourceMetadata { Url = "https://elastic.example.net", IndexName = "docs", Username = "reader", Password = Canary, ApiKey = Canary, Base64ApiKey = Canary });

        var postgres = new AIDataSource { ItemId = "postgres", DisplayText = "Postgres", Source = AIDataSourceSourceTypes.PostgreSQL };
        postgres.Put(new PostgreSQLSourceMetadata { TableName = "public.articles", ConnectionString = $"Host=db;Password={Canary}" });

        var connected = new AIDataSource { ItemId = "connected", DisplayText = "Connected", Source = AIDataSourceSourceTypes.AzureAISearch };
        connected.Put(new DataConnectionReference { ConnectionId = "conn-1" });

        _dataSources.Add(azure);
        _dataSources.Add(elastic);
        _dataSources.Add(postgres);
        _dataSources.Add(connected);
    }

    private ServiceProvider BuildServices(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);
        var indexStore = new Mock<IIndexProfileStore>();
        indexStore.Setup(store => store.GetByTypeAsync(DataSourceConstants.IndexingTaskType))
            .ReturnsAsync([new IndexProfile { Name = "kb", ProviderName = "Elasticsearch", Type = DataSourceConstants.IndexingTaskType }]);

        services.AddSingleton<ISourceCatalogManager<AIDataSource>>(_dataSources);
        services.AddSingleton(_queue.Object);
        services.AddSingleton(indexStore.Object);
        services.Configure<AIDataSourceSourceOptions>(options => options.AddOrUpdate(
            AIDataSourceSourceTypes.SearchIndexProfile,
            new LocalizedString("Search index", "Search index"),
            new LocalizedString("Search index", "Reads a search index.")));

        return services.BuildServiceProvider();
    }

    private static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            ListAIDataSourcesTool.TheName => new ListAIDataSourcesTool(),
            SyncAIDataSourceTool.TheName => new SyncAIDataSourceTool(),
            _ => new SearchAIDataSourceTool(),
        };
}
