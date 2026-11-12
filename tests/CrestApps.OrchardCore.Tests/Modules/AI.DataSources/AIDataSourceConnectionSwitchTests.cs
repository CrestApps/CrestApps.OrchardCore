using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.Drivers;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.ViewModels;
using CrestApps.OrchardCore.AI.DataSources.Deployments;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Drivers;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Models;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.ViewModels;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.Tests.DataConnectors;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

/// <summary>
/// Picking a data connection for an Azure AI Search or Elasticsearch data source keeps the data source's own endpoint
/// and credentials stored, so switching back to its own settings loses nothing; the credentials are still never
/// exported while the connection is used.
/// </summary>
public sealed class AIDataSourceConnectionSwitchTests
{
    private const string ConnectionId = "4qkc7h2m9x1r5t8v3w6y0z2b4d";

    private readonly EphemeralDataProtectionProvider _protection = new();

    [Fact]
    public async Task AzureAISearch_PickingAConnection_KeepsTheOwnEndpointAndKey_ForSwitchingBack()
    {
        // Arrange
        var connection = new DataConnection { ItemId = ConnectionId, Name = "azure-search", Connector = DataConnectorConstants.Connectors.HttpApi };
        connection.Put(new HttpConnectionSettings
        {
            BaseUrl = "https://contoso.search.windows.net",
            Authentication = new HttpAuthenticationSettings { Type = HttpAuthenticationType.ApiKey, ApiKeyName = "api-key" },
        });

        var driver = new AzureAISearchAIDataSourceDisplayDriver(_protection, CreatePicker(connection), new CrestApps.OrchardCore.AI.Core.AIEndpointPolicy(Moq.Mock.Of<Microsoft.Extensions.Hosting.IHostEnvironment>(), Microsoft.Extensions.Options.Options.Create(new CrestApps.OrchardCore.AI.Core.AIEgressOptions())), new PassThroughStringLocalizer<AzureAISearchAIDataSourceDisplayDriver>());
        var protectedKey = _protection.ProtectSetting(AIDataSourceProtectionConstants.SourceSecretPurpose, "own-key");
        var dataSource = new AIDataSource { ItemId = "ds-1", Source = AIDataSourceSourceTypes.AzureAISearch };
        dataSource.Put(new AzureAISearchSourceMetadata
        {
            Endpoint = "https://own.search.windows.net",
            AuthenticationType = AzureAISearchSourceMetadata.ApiKeyAuthenticationType,
            IndexName = "hotels",
            ApiKey = protectedKey,
        });

        // Act: pick the connection.
        var useConnection = Context(new EditAzureAISearchAIDataSourceViewModel { DataConnectionId = ConnectionId, IndexName = "hotels-v2" });
        await driver.UpdateAsync(dataSource, useConnection);

        // Assert
        Assert.True(useConnection.Updater.ModelState.IsValid);
        Assert.Equal(ConnectionId, AIDataSourceConnectionPicker.GetConnectionId(dataSource));
        var kept = dataSource.GetOrCreate<AzureAISearchSourceMetadata>();
        Assert.Equal("hotels-v2", kept.IndexName);
        Assert.Equal("https://own.search.windows.net", kept.Endpoint);
        Assert.Equal(protectedKey, kept.ApiKey);
        Assert.Equal(AzureAISearchSourceMetadata.ApiKeyAuthenticationType, kept.AuthenticationType);

        // The connection is in use, so its key is not exported, and neither is the stored one.
        var json = await ExportAsync(dataSource);
        Assert.DoesNotContain(protectedKey, json, StringComparison.Ordinal);

        // Act: switch back without typing the key again.
        var useOwn = Context(new EditAzureAISearchAIDataSourceViewModel
        {
            Endpoint = "https://own.search.windows.net",
            AuthenticationType = AzureAISearchSourceMetadata.ApiKeyAuthenticationType,
            IndexName = "hotels-v2",
        });
        await driver.UpdateAsync(dataSource, useOwn);

        // Assert
        Assert.True(useOwn.Updater.ModelState.IsValid);
        Assert.Null(AIDataSourceConnectionPicker.GetConnectionId(dataSource));
        Assert.Equal(protectedKey, dataSource.GetOrCreate<AzureAISearchSourceMetadata>().ApiKey);
    }

    [Fact]
    public async Task Elasticsearch_PickingAConnection_KeepsTheOwnUrlAndPassword_ForSwitchingBack()
    {
        // Arrange
        var connection = new DataConnection { ItemId = ConnectionId, Name = "search", Connector = DataConnectorConstants.Connectors.Elasticsearch };
        connection.Put(new HttpConnectionSettings
        {
            BaseUrl = "https://search.example.com:9200",
            Authentication = new HttpAuthenticationSettings { Type = HttpAuthenticationType.Basic, UserName = "elastic" },
        });

        var driver = new ElasticsearchAIDataSourceDisplayDriver(_protection, Options.Create(new ElasticsearchDataSourceOptions()), CreatePicker(connection), new PassThroughStringLocalizer<ElasticsearchAIDataSourceDisplayDriver>());
        var protectedPassword = _protection.ProtectSetting(AIDataSourceProtectionConstants.SourceSecretPurpose, "own-pass");
        var dataSource = new AIDataSource { ItemId = "ds-2", Source = AIDataSourceSourceTypes.Elasticsearch };
        dataSource.Put(new ElasticsearchSourceMetadata
        {
            EnvironmentType = ElasticsearchSourceMetadata.SelfManagedEnvironmentType,
            Url = "http://own:9200",
            AuthenticationType = ElasticsearchSourceMetadata.BasicAuthenticationType,
            Username = "own-user",
            Password = protectedPassword,
            IndexName = "articles",
        });

        // Act: pick the connection.
        await driver.UpdateAsync(dataSource, Context(new EditElasticsearchAIDataSourceViewModel { DataConnectionId = ConnectionId, IndexName = "articles-v2" }));

        // Assert
        var kept = dataSource.GetOrCreate<ElasticsearchSourceMetadata>();
        Assert.Equal(ConnectionId, AIDataSourceConnectionPicker.GetConnectionId(dataSource));
        Assert.Equal("articles-v2", kept.IndexName);
        Assert.Equal("http://own:9200", kept.Url);
        Assert.Equal("own-user", kept.Username);
        Assert.Equal(protectedPassword, kept.Password);

        var json = await ExportAsync(dataSource);
        Assert.DoesNotContain(protectedPassword, json, StringComparison.Ordinal);

        // Act: switch back without typing the password again.
        var useOwn = Context(new EditElasticsearchAIDataSourceViewModel
        {
            EnvironmentType = ElasticsearchSourceMetadata.SelfManagedEnvironmentType,
            Url = "http://own:9200",
            AuthenticationType = ElasticsearchSourceMetadata.BasicAuthenticationType,
            Username = "own-user",
            IndexName = "articles-v2",
        });
        await driver.UpdateAsync(dataSource, useOwn);

        // Assert
        Assert.True(useOwn.Updater.ModelState.IsValid);
        Assert.Equal(protectedPassword, dataSource.GetOrCreate<ElasticsearchSourceMetadata>().Password);
    }

    private static UpdateEditorContext Context(object model)
        => new(null, string.Empty, false, string.Empty, null, null, new FakeUpdateModel(model));

    private static AIDataSourceConnectionPicker CreatePicker(DataConnection connection)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("test")) } };

        return new AIDataSourceConnectionPicker(authorization.Object, accessor, new PassThroughStringLocalizer<AIDataSourceConnectionPicker>(), new SingleConnectionResolver(connection));
    }

    private static async Task<string> ExportAsync(AIDataSource dataSource)
    {
        var store = new Mock<IAIDataSourceStore>();
        store
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<AIDataSource>>([dataSource]));

        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());
        await new AIDataSourceDeploymentSource(store.Object).ProcessDeploymentStepAsync(new AIDataSourceDeploymentStep { IncludeAll = true }, result);

        return Assert.Single(result.Steps).ToJsonString();
    }

    private sealed class SingleConnectionResolver : IDataConnectionResolver
    {
        private readonly DataConnection _connection;

        public SingleConnectionResolver(DataConnection connection)
        {
            _connection = connection;
        }

        public Task<IReadOnlyList<DataConnection>> ListAsync(IEnumerable<string> connectorNames, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DataConnection>>([_connection]);

        public Task<DataConnectorContext> ResolveAsync(string connectionId, CancellationToken cancellationToken = default)
            => Task.FromResult(_connection.ItemId == connectionId ? new DataConnectorContext(_connection, new FakeSecrets()) : null);
    }
}
