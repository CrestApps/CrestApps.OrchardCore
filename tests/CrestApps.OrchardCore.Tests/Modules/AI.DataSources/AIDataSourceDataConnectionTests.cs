using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;
using CrestApps.OrchardCore.AI.DataSources.Deployments;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Models;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.Tests.DataConnectors;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

/// <summary>
/// AI data sources that reach their external index through a data connection (P10), and the ones that keep their own
/// endpoint and credentials.
/// </summary>
public sealed class AIDataSourceDataConnectionTests
{
    private const string ConnectionId = "4qkc7h2m9x1r5t8v3w6y0z2b4d";

    private readonly EphemeralDataProtectionProvider _protection = new();

    [Fact]
    public async Task Elasticsearch_ResolvesTheUrlAndCredentials_FromTheConnection()
    {
        var resolver = new FakeResolver(ElasticsearchConnection(), new FakeSecrets(new() { [HttpAuthenticationSecrets.Password] = "conn-pass" }));
        var handler = CreateElasticsearchHandler(resolver);
        var dataSource = ElasticsearchSourceUsingConnection();

        var result = new ValidationResultDetails();
        await handler.ValidateAsync(dataSource, result, TestContext.Current.CancellationToken);
        var (client, metadata) = await handler.ResolveAsync(dataSource, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("products", metadata.IndexName);
        Assert.Equal(new Uri("https://search.example.com:9200/"), Assert.Single(client.ElasticsearchClientSettings.NodePool.Nodes).Uri);
        var basic = Assert.IsType<BasicAuthentication>(client.ElasticsearchClientSettings.Authentication);
        Assert.True(basic.TryGetAuthorizationParameters(out var parameters));
        Assert.Equal(Convert.ToBase64String("elastic:conn-pass"u8.ToArray()), parameters);
    }

    [Fact]
    public async Task Elasticsearch_FallsBackToTheEmbeddedSettings_WhenNoConnectionIsReferenced()
    {
        var handler = CreateElasticsearchHandler(new FakeResolver(ElasticsearchConnection(), new FakeSecrets()));
        var dataSource = new AIDataSource { ItemId = "ds-1", Source = AIDataSourceSourceTypes.Elasticsearch };
        dataSource.Put(new ElasticsearchSourceMetadata
        {
            Url = "http://embedded:9200",
            IndexName = "articles",
        });

        var result = new ValidationResultDetails();
        await handler.ValidateAsync(dataSource, result, TestContext.Current.CancellationToken);
        var (client, _) = await handler.ResolveAsync(dataSource, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(new Uri("http://embedded:9200/"), Assert.Single(client.ElasticsearchClientSettings.NodePool.Nodes).Uri);
    }

    [Fact]
    public async Task Elasticsearch_FailsValidation_WhenTheConnectionIsGoneOrTheFeatureIsOff()
    {
        var dataSource = ElasticsearchSourceUsingConnection();

        var missing = new ValidationResultDetails();
        await CreateElasticsearchHandler(new FakeResolver(null, new FakeSecrets())).ValidateAsync(dataSource, missing, TestContext.Current.CancellationToken);

        var featureOff = new ValidationResultDetails();
        await CreateElasticsearchHandler(null).ValidateAsync(dataSource, featureOff, TestContext.Current.CancellationToken);

        Assert.False(missing.Succeeded);
        Assert.False(featureOff.Succeeded);
    }

    [Fact]
    public async Task Elasticsearch_RefusesAConnectionOfAnotherConnector()
    {
        var connection = ElasticsearchConnection();
        connection.Connector = DataConnectorConstants.Connectors.SqlServer;

        var result = new ValidationResultDetails();
        await CreateElasticsearchHandler(new FakeResolver(connection, new FakeSecrets())).ValidateAsync(ElasticsearchSourceUsingConnection(), result, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ElasticsearchSettings_SendAnApiKeyInTheAuthorizationHeader()
    {
        var settings = new HttpConnectionSettings
        {
            BaseUrl = "https://search.example.com",
            Authentication = new HttpAuthenticationSettings
            {
                Type = HttpAuthenticationType.ApiKey,
                ApiKeyName = "Authorization",
                ApiKeyPrefix = "ApiKey ",
            },
        };

        IElasticsearchClientSettings clientSettings = ElasticsearchDataConnectionSettings.Create(settings, new FakeSecrets(new() { [HttpAuthenticationSecrets.ApiKey] = "a2V5" }));

        Assert.Equal("ApiKey", clientSettings.Authentication.AuthScheme);
        Assert.True(clientSettings.Authentication.TryGetAuthorizationParameters(out var parameters));
        Assert.Equal("a2V5", parameters);
    }

    [Fact]
    public void ElasticsearchSettings_RefuseAnAuthenticationTheClientCannotSend()
    {
        var settings = new HttpConnectionSettings
        {
            BaseUrl = "https://search.example.com",
            Authentication = new HttpAuthenticationSettings { Type = HttpAuthenticationType.OAuth2 },
        };

        Assert.Throws<InvalidOperationException>(() => ElasticsearchDataConnectionSettings.Create(settings, new FakeSecrets()));
    }

    [Fact]
    public async Task AzureAISearch_ResolvesTheEndpointAndKey_FromTheConnection()
    {
        var connection = new DataConnection
        {
            ItemId = ConnectionId,
            Name = "azure-search",
            Connector = DataConnectorConstants.Connectors.HttpApi,
        };
        connection.Put(new HttpConnectionSettings
        {
            BaseUrl = "https://contoso.search.windows.net",
            Authentication = new HttpAuthenticationSettings { Type = HttpAuthenticationType.ApiKey, ApiKeyName = "api-key" },
        });

        var handler = CreateAzureHandler(new FakeResolver(connection, new FakeSecrets(new() { [HttpAuthenticationSecrets.ApiKey] = "service-key" })));
        var dataSource = new AIDataSource { ItemId = "ds-2", Source = AIDataSourceSourceTypes.AzureAISearch };
        dataSource.Put(new AzureAISearchSourceMetadata { IndexName = "hotels" });
        dataSource.Put(new DataConnectionReference { ConnectionId = ConnectionId });

        var result = new ValidationResultDetails();
        await handler.ValidateAsync(dataSource, result, TestContext.Current.CancellationToken);
        var client = await handler.ResolveAsync(dataSource, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(new Uri("https://contoso.search.windows.net"), client.Endpoint);
        Assert.Equal("hotels", client.IndexName);
    }

    [Fact]
    public async Task AzureAISearch_FallsBackToTheEmbeddedEndpointAndKey_WhenNoConnectionIsReferenced()
    {
        var handler = CreateAzureHandler(null);
        var dataSource = new AIDataSource { ItemId = "ds-3", Source = AIDataSourceSourceTypes.AzureAISearch };
        dataSource.Put(new AzureAISearchSourceMetadata
        {
            Endpoint = "https://embedded.search.windows.net",
            IndexName = "hotels",
            AuthenticationType = AzureAISearchSourceMetadata.ApiKeyAuthenticationType,
            ApiKey = _protection.ProtectSetting(AIDataSourceProtectionConstants.SourceSecretPurpose, "embedded-key"),
        });

        var result = new ValidationResultDetails();
        await handler.ValidateAsync(dataSource, result, TestContext.Current.CancellationToken);
        var client = await handler.ResolveAsync(dataSource, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(new Uri("https://embedded.search.windows.net"), client.Endpoint);
    }

    [Fact]
    public void AzureAISearchClient_RefusesABearerTokenConnection()
    {
        var settings = new HttpConnectionSettings
        {
            BaseUrl = "https://contoso.search.windows.net",
            Authentication = new HttpAuthenticationSettings { Type = HttpAuthenticationType.Bearer },
        };

        Assert.Throws<InvalidOperationException>(() => AzureAISearchDataConnectionClient.Create(settings, new FakeSecrets(), "hotels"));
    }

    [Fact]
    public async Task Export_OfADataSourceUsingAConnection_CarriesTheReferenceAndNoCredentials()
    {
        var dataSource = ElasticsearchSourceUsingConnection();
        var store = new Mock<IAIDataSourceStore>();
        store
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<AIDataSource>>([dataSource]));

        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());
        await new AIDataSourceDeploymentSource(store.Object).ProcessDeploymentStepAsync(new AIDataSourceDeploymentStep { IncludeAll = true }, result);

        var step = Assert.Single(result.Steps);
        var exported = Assert.Single(step["DataSources"]!.AsArray())!.AsObject();
        var properties = exported["Properties"]!.AsObject();
        var json = step.ToJsonString();

        Assert.Equal(ConnectionId, properties[nameof(DataConnectionReference)]![nameof(DataConnectionReference.ConnectionId)]!.GetValue<string>());
        Assert.Null(properties[nameof(ElasticsearchSourceMetadata)]![nameof(ElasticsearchSourceMetadata.Password)]?.GetValue<string>());
        Assert.DoesNotContain("conn-pass", json, StringComparison.Ordinal);
        Assert.DoesNotContain("search.example.com", json, StringComparison.Ordinal);
    }

    private static AIDataSource ElasticsearchSourceUsingConnection()
    {
        var dataSource = new AIDataSource { ItemId = "ds-1", Source = AIDataSourceSourceTypes.Elasticsearch };
        dataSource.Put(new ElasticsearchSourceMetadata { IndexName = "products" });
        dataSource.Put(new DataConnectionReference { ConnectionId = ConnectionId });

        return dataSource;
    }

    private static DataConnection ElasticsearchConnection()
    {
        var connection = new DataConnection
        {
            ItemId = ConnectionId,
            Name = "search",
            Connector = DataConnectorConstants.Connectors.Elasticsearch,
        };
        connection.Put(new HttpConnectionSettings
        {
            BaseUrl = "https://search.example.com:9200",
            Authentication = new HttpAuthenticationSettings { Type = HttpAuthenticationType.Basic, UserName = "elastic" },
        });

        return connection;
    }

    private static ElasticsearchAIDataSourceSourceHandler CreateElasticsearchHandler(IDataConnectionResolver resolver)
        => new(
            Mock.Of<IDataProtectionProvider>(),
            Options.Create(new ElasticsearchDataSourceOptions()),
            NullLogger<ElasticsearchAIDataSourceSourceHandler>.Instance,
            resolver);

    private AzureAISearchAIDataSourceSourceHandler CreateAzureHandler(IDataConnectionResolver resolver)
        => new(_protection, NullLogger<AzureAISearchAIDataSourceSourceHandler>.Instance, resolver);

    private sealed class FakeResolver : IDataConnectionResolver
    {
        private readonly DataConnection _connection;
        private readonly IDataConnectorSecrets _secrets;

        public FakeResolver(DataConnection connection, IDataConnectorSecrets secrets)
        {
            _connection = connection;
            _secrets = secrets;
        }

        public Task<IReadOnlyList<DataConnection>> ListAsync(IEnumerable<string> connectorNames, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DataConnection>>(_connection is null ? [] : [_connection]);

        public Task<DataConnectorContext> ResolveAsync(string connectionId, CancellationToken cancellationToken = default)
            => Task.FromResult(_connection is not null && _connection.ItemId == connectionId ? new DataConnectorContext(_connection, _secrets) : null);
    }
}
