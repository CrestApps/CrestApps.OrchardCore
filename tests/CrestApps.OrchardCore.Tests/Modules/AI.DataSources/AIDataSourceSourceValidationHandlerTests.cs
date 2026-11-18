using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Models;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;
using CrestApps.OrchardCore.AI.DataSources.Handlers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

/// <summary>
/// Review 7 (Mild): the endpoint check sat in each source handler's <c>ValidateAsync</c>, which no save path called, so
/// <c>POST api/ai/v1/data-sources</c> with <c>http://10.0.0.5/</c> was saved and synced. The API and the recipe step
/// validate through the catalog, so a catalog handler now runs the source handler's checks.
/// </summary>
public sealed class AIDataSourceSourceValidationHandlerTests
{
    [Theory]
    [InlineData("http://10.0.0.5/")]
    [InlineData("http://localhost:9200/")]
    public async Task Validating_AnAzureAISearchSourceOnTheSitesOwnNetwork_Fails(string endpoint)
    {
        // Arrange
        await using var services = Services();
        var dataSource = AzureSource(endpoint, apiKey: "key");

        // Act
        var result = await ValidateAsync(services, dataSource);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.MemberNames.Contains(nameof(AzureAISearchSourceMetadata.Endpoint)));
    }

    [Fact]
    public async Task Validating_AnAzureAISearchSourceWithoutItsApiKey_Fails()
    {
        // Arrange: the handlers' "API key is required" checks were dead for the same reason.
        await using var services = Services();
        var dataSource = AzureSource("https://search.vendor.example/", apiKey: null);

        // Act
        var result = await ValidateAsync(services, dataSource);

        // Assert
        Assert.Contains(result.Errors, error => error.MemberNames.Contains(nameof(AzureAISearchSourceMetadata.ApiKey)));
    }

    [Fact]
    public async Task Validating_AnElasticsearchSourceOnTheSitesOwnNetwork_Fails()
    {
        // Arrange
        await using var services = Services();
        var dataSource = new AIDataSource { ItemId = "ds-2", Source = AIDataSourceSourceTypes.Elasticsearch, DisplayText = "Logs" };
        dataSource.Properties[nameof(ElasticsearchSourceMetadata)] = new JsonObject
        {
            ["EnvironmentType"] = ElasticsearchSourceMetadata.SelfManagedEnvironmentType,
            ["Url"] = "http://192.168.1.20:9200/",
            ["AuthenticationType"] = ElasticsearchSourceMetadata.NoneAuthenticationType,
            ["IndexName"] = "logs",
        };

        // Act
        var result = await ValidateAsync(services, dataSource);

        // Assert
        Assert.Contains(result.Errors, error => error.MemberNames.Contains(nameof(ElasticsearchSourceMetadata.Url)));
    }

    [Fact]
    public async Task Saving_AStoredElasticsearchSourceOnAPrivateNetwork_ThatKeepsItsUrl_Succeeds()
    {
        // Arrange (review 8, G7): a self-managed cluster on a private network, saved before the check, could no longer be
        // edited by the API or a recipe, while it kept syncing. The recipe step changes the record it read, then validates.
        await using var services = Services();
        await using var scope = services.CreateAsyncScope();
        var store = Store(ElasticsearchSource("http://172.18.0.3:9200/"));
        var manager = Manager(scope, store);

        // Act
        var entry = await manager.FindByIdAsync("ds-2", TestContext.Current.CancellationToken);
        entry.Put(Metadata(entry.GetOrCreate<ElasticsearchSourceMetadata>().Url, indexName: "logs-v2"));
        var result = await manager.ValidateAsync(entry, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.ErrorMessage)));
    }

    [Theory]
    [InlineData("http://172.18.0.3:9200/")]
    [InlineData("http://10.0.0.5:9200/")]
    public async Task Saving_AnElasticsearchSource_ChecksAUrlOnlyWhenItChanges(string url)
    {
        // Arrange (review 8, G7): the API validates a copy of the record it read, with the record's id.
        await using var services = Services();
        await using var scope = services.CreateAsyncScope();
        var manager = Manager(scope, Store(ElasticsearchSource("http://172.18.0.3:9200/")));
        await manager.FindByIdAsync("ds-2", TestContext.Current.CancellationToken);
        var copy = ElasticsearchSource(url);

        // Act
        var result = await manager.ValidateAsync(copy, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(url == "http://172.18.0.3:9200/", result.Succeeded);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "the-password")]
    [InlineData("elastic", null)]
    public async Task Validating_ABasicElasticsearchSource_RequiresAUsernameAndAPassword(string username, string password)
    {
        // Arrange (review 9, K1): a blank user name with no password passed, so a move that dropped the password was saved.
        await using var services = Services();
        var dataSource = ElasticsearchSource("https://search.vendor.example:9200/");
        var metadata = Metadata("https://search.vendor.example:9200/", indexName: "logs");
        metadata.AuthenticationType = ElasticsearchSourceMetadata.BasicAuthenticationType;
        metadata.Username = username;
        metadata.Password = password;
        dataSource.Put(metadata);

        // Act
        var result = await ValidateAsync(services, dataSource);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.MemberNames.Contains(username is null ? nameof(ElasticsearchSourceMetadata.Username) : nameof(ElasticsearchSourceMetadata.Password)));
    }

    [Fact]
    public async Task Validating_APublicAzureAISearchSource_Succeeds_AndASourceWithoutAHandlerIsLeftAlone()
    {
        // Arrange
        await using var services = Services();

        // Act
        var valid = await ValidateAsync(services, AzureSource("https://search.vendor.example/", apiKey: "key"));
        var unknown = await ValidateAsync(services, new AIDataSource { ItemId = "ds-3", Source = "Web", DisplayText = "Site" });

        // Assert
        Assert.True(valid.Succeeded);
        Assert.True(unknown.Succeeded);
    }

    [Fact]
    public void Startup_RegistersTheHandlerWithTheCatalog()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new CrestApps.OrchardCore.AI.DataSources.Startup().ConfigureServices(services);

        // Assert
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ICatalogEntryHandler<AIDataSource>) &&
            descriptor.ImplementationType == typeof(AIDataSourceSourceValidationHandler));
    }

    // The source handlers as the modules register them, and the catalog handler, resolved from the
    // container as the catalog manager resolves its handlers.
    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton(Options.Create(new ElasticsearchDataSourceOptions()));
        services.AddAIDataSourceSourceHandler<AzureAISearchAIDataSourceSourceHandler>(AIDataSourceSourceTypes.AzureAISearch);
        services.AddAIDataSourceSourceHandler<ElasticsearchAIDataSourceSourceHandler>(AIDataSourceSourceTypes.Elasticsearch);
        services.AddScoped<ICatalogEntryHandler<AIDataSource>, AIDataSourceSourceValidationHandler>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static async Task<ValidationResultDetails> ValidateAsync(ServiceProvider services, AIDataSource dataSource)
    {
        await using var scope = services.CreateAsyncScope();
        var context = new ValidatingContext<AIDataSource>(dataSource);

        foreach (var handler in scope.ServiceProvider.GetServices<ICatalogEntryHandler<AIDataSource>>())
        {
            await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);
        }

        return context.Result;
    }

    // The data source manager the site uses, over a store that returns the instance it keeps, as a session does.
    private static DefaultAIDataSourceManager Manager(AsyncServiceScope scope, IAIDataSourceStore store)
        => new(store, scope.ServiceProvider.GetServices<ICatalogEntryHandler<AIDataSource>>(), NullLogger<DefaultAIDataSourceManager>.Instance);

    private static IAIDataSourceStore Store(AIDataSource stored)
    {
        var store = new Mock<IAIDataSourceStore>();
        store
            .Setup(x => x.FindByIdAsync(stored.ItemId, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(stored));

        return store.Object;
    }

    private static AIDataSource ElasticsearchSource(string url)
    {
        var dataSource = new AIDataSource { ItemId = "ds-2", Source = AIDataSourceSourceTypes.Elasticsearch, DisplayText = "Logs" };
        dataSource.Put(Metadata(url, indexName: "logs"));

        return dataSource;
    }

    private static ElasticsearchSourceMetadata Metadata(string url, string indexName)
        => new()
        {
            EnvironmentType = ElasticsearchSourceMetadata.SelfManagedEnvironmentType,
            Url = url,
            AuthenticationType = ElasticsearchSourceMetadata.NoneAuthenticationType,
            IndexName = indexName,
        };

    private static AIDataSource AzureSource(string endpoint, string apiKey)
    {
        var dataSource = new AIDataSource { ItemId = "ds-1", Source = AIDataSourceSourceTypes.AzureAISearch, DisplayText = "Policies" };
        dataSource.Properties[nameof(AzureAISearchSourceMetadata)] = new JsonObject
        {
            ["Endpoint"] = endpoint,
            ["AuthenticationType"] = AzureAISearchSourceMetadata.ApiKeyAuthenticationType,
            ["ApiKey"] = apiKey,
            ["IndexName"] = "policies",
        };

        return dataSource;
    }
}
