using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Drivers;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Models;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.ViewModels;
using CrestApps.OrchardCore.AI.DataSources.Handlers;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

/// <summary>
/// Review 10 (A3): an Elastic Cloud data source checked only that a Cloud ID was present. The client derives the
/// cluster's address from the ID (base64 <c>host$es-uuid$kibana-uuid</c>), so the ID could name any host, the site's own
/// network included. The address the ID decodes to now goes through the same policy as a self-managed URL, at save
/// (editor, API and recipe) and before the client connects.
/// </summary>
public sealed class ElasticCloudIdEgressTests
{
    // An Elastic Cloud deployment: the client connects to https://abc123.us-central1.gcp.cloud.es.io.
    private static readonly string _publicCloudId = CloudId("us-central1.gcp.cloud.es.io$abc123$def456");

    // A crafted ID whose host resolves to the site's own network.
    private static readonly string _insideCloudId = CloudId("inside.example$abc123");

    // The same, with a port, as the client allows.
    private static readonly string _insideCloudIdWithPort = CloudId("inside.example:9243$abc123");

    private readonly EphemeralDataProtectionProvider _protection = new();

    [Fact]
    public async Task Saving_ANewCloudSource_WhoseIdNamesAnInsideHost_Fails()
    {
        // Arrange: the API and the recipe step validate through the catalog.
        await using var services = Services();
        await using var scope = services.CreateAsyncScope();
        var manager = Manager(scope, Store(null));

        // Act
        var inside = await manager.ValidateAsync(CloudSource(_insideCloudId), TestContext.Current.CancellationToken);
        var insideWithPort = await manager.ValidateAsync(CloudSource(_insideCloudIdWithPort), TestContext.Current.CancellationToken);
        var outside = await manager.ValidateAsync(CloudSource(_publicCloudId), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(inside.Errors, error => error.MemberNames.Contains(nameof(ElasticsearchSourceMetadata.CloudId)));
        Assert.Contains(insideWithPort.Errors, error => error.MemberNames.Contains(nameof(ElasticsearchSourceMetadata.CloudId)));
        Assert.True(outside.Succeeded, string.Join(" ", outside.Errors.Select(error => error.ErrorMessage)));
    }

    [Fact]
    public async Task Saving_ACloudSource_WhoseIdIsNotACloudId_Fails()
    {
        // Arrange
        await using var services = Services();
        await using var scope = services.CreateAsyncScope();
        var manager = Manager(scope, Store(null));

        // Act
        var result = await manager.ValidateAsync(CloudSource("deployment:not-base64!"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.MemberNames.Contains(nameof(ElasticsearchSourceMetadata.CloudId)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Saving_ACloudSource_ChecksTheIdOnlyWhenItChanges(bool keepsTheId)
    {
        // Arrange (review 8, G7, and review 9, K6): a Cloud ID the stored source used is not checked again; a changed one is.
        await using var services = Services();
        await using var scope = services.CreateAsyncScope();
        var manager = Manager(scope, Store(CloudSource(_insideCloudId)));
        await manager.FindByIdAsync("ds-2", TestContext.Current.CancellationToken);

        // Act
        var result = await manager.ValidateAsync(CloudSource(keepsTheId ? _insideCloudId : _insideCloudIdWithPort), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(keepsTheId, result.Succeeded);
    }

    [Fact]
    public async Task Saving_ACloudSource_ChecksAnIdStoredWhileItWasNotUsed()
    {
        // Arrange (review 9, K6): the API stored the ID while the source was self-managed, so it was never checked.
        await using var services = Services();
        await using var scope = services.CreateAsyncScope();
        var stored = CloudSource(_insideCloudId);
        var metadata = stored.GetOrCreate<ElasticsearchSourceMetadata>();
        metadata.EnvironmentType = ElasticsearchSourceMetadata.SelfManagedEnvironmentType;
        metadata.Url = "https://search.vendor.example:9200/";
        stored.Put(metadata);
        var manager = Manager(scope, Store(stored));
        await manager.FindByIdAsync("ds-2", TestContext.Current.CancellationToken);

        // Act
        var result = await manager.ValidateAsync(CloudSource(_insideCloudId), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.MemberNames.Contains(nameof(ElasticsearchSourceMetadata.CloudId)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Editor_ChecksTheCloudId_WhenItIsNewOrChanged(bool storedTheSameId, bool isValid)
    {
        // Arrange
        var driver = Driver();
        var dataSource = new AIDataSource { ItemId = "ds-2", Source = AIDataSourceSourceTypes.Elasticsearch };

        if (storedTheSameId)
        {
            dataSource.Put(CloudMetadata(_insideCloudId, _protection.ProtectSetting(AIDataSourceProtectionConstants.SourceSecretPurpose, "the-password")));
        }

        var context = Context(new EditElasticsearchAIDataSourceViewModel
        {
            EnvironmentType = ElasticsearchSourceMetadata.CloudHostedEnvironmentType,
            CloudId = _insideCloudId,
            AuthenticationType = ElasticsearchSourceMetadata.BasicAuthenticationType,
            Username = "elastic",
            Password = storedTheSameId ? null : "the-password",
            IndexName = "articles",
        });

        // Act
        await driver.UpdateAsync(dataSource, context);

        // Assert
        Assert.Equal(isValid, context.Updater.ModelState.IsValid);

        if (!isValid)
        {
            Assert.Contains(context.Updater.ModelState, entry => entry.Key.EndsWith(nameof(EditElasticsearchAIDataSourceViewModel.CloudId), StringComparison.Ordinal) && entry.Value.Errors.Count > 0);
        }
    }

    [Fact]
    public async Task Editor_AcceptsACloudIdOutsideTheSitesNetwork()
    {
        // Arrange
        var driver = Driver();
        var dataSource = new AIDataSource { ItemId = "ds-2", Source = AIDataSourceSourceTypes.Elasticsearch };
        var context = Context(new EditElasticsearchAIDataSourceViewModel
        {
            EnvironmentType = ElasticsearchSourceMetadata.CloudHostedEnvironmentType,
            CloudId = _publicCloudId,
            AuthenticationType = ElasticsearchSourceMetadata.BasicAuthenticationType,
            Username = "elastic",
            Password = "the-password",
            IndexName = "articles",
        });

        // Act
        await driver.UpdateAsync(dataSource, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
    }

    [Fact]
    public async Task Connecting_ToACloudIdThatNamesAnInsideHost_Throws_BeforeTheClientIsBuilt()
    {
        // Arrange: a source stored before the check, or by a path that skipped it, is refused when it syncs.
        var handler = Handler();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ResolveAsync(CloudSource(_insideCloudId), TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("own network", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Connecting_ToACloudIdOutsideTheSitesNetwork_ReachesTheDecodedAddress()
    {
        // Arrange
        var handler = Handler();

        // Act
        var (client, _) = await handler.ResolveAsync(CloudSource(_publicCloudId), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Uri("https://abc123.us-central1.gcp.cloud.es.io/"), Assert.Single(client.ElasticsearchClientSettings.NodePool.Nodes).Uri);
    }

    [Theory]
    [InlineData("us-central1.gcp.cloud.es.io$abc123$def456")]
    [InlineData("us-central1.gcp.cloud.es.io:9243$abc123")]
    [InlineData("us-central1.gcp.cloud.es.io$abc123:9244$def456")]
    [InlineData(" inside.example $ abc123 ")]
    [InlineData("user@inside.example$abc123")]
    public void ElasticCloudId_DecodesTheAddressTheClientConnectsTo(string decoded)
    {
        // Arrange: the check must judge the address the transport derives, not another reading of the ID.
        var cloudId = CloudId(decoded);
        var transport = new Elastic.Transport.CloudNodePool(cloudId, new Elastic.Transport.BasicAuthentication("elastic", "the-password"));

        // Act
        var decodedOk = ElasticCloudId.TryGetEndpoint(cloudId, out var endpoint);

        // Assert
        Assert.True(decodedOk);
        Assert.Equal(Assert.Single(transport.Nodes).Uri, endpoint);
    }

    private static string CloudId(string decoded)
        => "deployment:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(decoded));

    // The host names the tests use, resolved as a DNS server would answer them.
    private static IPAddress[] Resolve(string host)
        => host switch
        {
            "abc123.inside.example" => [IPAddress.Parse("10.0.0.5")],
            "abc123.us-central1.gcp.cloud.es.io" => [IPAddress.Parse("34.120.127.130")],
            _ => throw new SocketException((int)SocketError.HostNotFound),
        };

    private static AIEndpointPolicy EndpointPolicy()
        => new(Mock.Of<IHostEnvironment>(), Options.Create(new AIEgressOptions()), Resolve);

    private ElasticsearchAIDataSourceSourceHandler Handler()
        => new(_protection, Options.Create(new ElasticsearchDataSourceOptions()), NullLogger<ElasticsearchAIDataSourceSourceHandler>.Instance, endpointPolicy: EndpointPolicy());

    private ElasticsearchAIDataSourceDisplayDriver Driver()
        => new(_protection, Options.Create(new ElasticsearchDataSourceOptions()), CreatePicker(), EndpointPolicy(), new PassThroughStringLocalizer<ElasticsearchAIDataSourceDisplayDriver>());

    // The source handler as the module registers it, the stored-settings copy, and the catalog handler, resolved from
    // the container as the catalog manager resolves its handlers.
    private ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDataProtectionProvider>(_protection);
        services.AddSingleton(Options.Create(new ElasticsearchDataSourceOptions()));
        services.AddSingleton<IAIEndpointPolicy>(EndpointPolicy());
        services.AddAIDataSourceSourceHandler<ElasticsearchAIDataSourceSourceHandler>(AIDataSourceSourceTypes.Elasticsearch);
        services.AddScoped<ICatalogEntryHandler<AIDataSource>, AIDataSourceSourceValidationHandler>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static DefaultAIDataSourceManager Manager(AsyncServiceScope scope, IAIDataSourceStore store)
        => new(store, scope.ServiceProvider.GetServices<ICatalogEntryHandler<AIDataSource>>(), NullLogger<DefaultAIDataSourceManager>.Instance);

    private static IAIDataSourceStore Store(AIDataSource stored)
    {
        var store = new Mock<IAIDataSourceStore>();

        if (stored is not null)
        {
            store
                .Setup(x => x.FindByIdAsync(stored.ItemId, It.IsAny<CancellationToken>()))
                .Returns(() => ValueTask.FromResult(stored));
        }

        return store.Object;
    }

    private AIDataSource CloudSource(string cloudId)
    {
        var dataSource = new AIDataSource { ItemId = "ds-2", Source = AIDataSourceSourceTypes.Elasticsearch, DisplayText = "Logs" };
        dataSource.Put(CloudMetadata(cloudId, _protection.ProtectSetting(AIDataSourceProtectionConstants.SourceSecretPurpose, "the-password")));

        return dataSource;
    }

    private static ElasticsearchSourceMetadata CloudMetadata(string cloudId, string protectedPassword)
        => new()
        {
            EnvironmentType = ElasticsearchSourceMetadata.CloudHostedEnvironmentType,
            CloudId = cloudId,
            AuthenticationType = ElasticsearchSourceMetadata.BasicAuthenticationType,
            Username = "elastic",
            Password = protectedPassword,
            IndexName = "articles",
        };

    private static UpdateEditorContext Context(object model)
        => new(null, string.Empty, false, string.Empty, null, null, new FakeUpdateModel(model));

    private static AIDataSourceConnectionPicker CreatePicker()
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("test")) } };

        return new AIDataSourceConnectionPicker(authorization.Object, accessor, new PassThroughStringLocalizer<AIDataSourceConnectionPicker>(), new NoConnectionResolver());
    }

    private sealed class NoConnectionResolver : IDataConnectionResolver
    {
        public Task<IReadOnlyList<DataConnection>> ListAsync(IEnumerable<string> connectorNames, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DataConnection>>([]);

        public Task<DataConnectorContext> ResolveAsync(string connectionId, CancellationToken cancellationToken = default)
            => Task.FromResult<DataConnectorContext>(null);
    }
}
