using System.Text.Json.Nodes;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Models;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;
using CrestApps.OrchardCore.AI.DataSources.Handlers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
