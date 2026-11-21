using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;
using CrestApps.OrchardCore.AI.DataSources.Handlers;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.Tests.DataConnectors;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

/// <summary>
/// Review 10 (B1, High): an Azure AI Search data source could sign in with a managed identity or the default Azure
/// credential, which are the host's. On a shared host whose identity reads several tenants' indexes, one tenant's
/// administrator could import another tenant's documents. As for the SQL Server connector, the host's identity now works
/// on the default tenant, and on another tenant only for a search service, index and identity the host approved for it in
/// the tenant's configuration; at save and before the client is built.
/// </summary>
public sealed class AzureAISearchHostIdentityTests
{
    private const string ConnectionId = "4qkc7h2m9x1r5t8v3w6y0z2b4d";

    private const string Endpoint = "https://shared.search.windows.net";

    [Theory]
    [InlineData(AzureAISearchSourceMetadata.ManagedIdentityAuthenticationType)]
    [InlineData(AzureAISearchSourceMetadata.DefaultAuthenticationType)]
    public async Task Saving_AHostIdentitySource_OnATenant_TheHostDidNotApprove_Fails(string authenticationType)
    {
        // Arrange: the editor, the API and the recipe step all validate through the catalog.
        await using var services = Services("tenant-a");

        // Act
        var result = await ValidateAsync(services, OwnSource(authenticationType, "tenant-b-docs"));

        // Assert
        Assert.Contains(result.Errors, error => error.MemberNames.Contains(nameof(AzureAISearchSourceMetadata.AuthenticationType)));
    }

    [Fact]
    public async Task Saving_AnApiKeySource_OnAnyTenant_NeedsNoApproval()
    {
        // Arrange
        await using var services = Services("tenant-a");
        var dataSource = OwnSource(AzureAISearchSourceMetadata.ApiKeyAuthenticationType, "tenant-a-docs");
        var metadata = dataSource.GetOrCreate<AzureAISearchSourceMetadata>();
        metadata.ApiKey = "the-key";
        dataSource.Put(metadata);

        // Act
        var result = await ValidateAsync(services, dataSource);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.ErrorMessage)));
    }

    [Fact]
    public async Task Connecting_AsTheHost_OnATenant_TheHostDidNotApprove_Throws_BeforeTheClientIsBuilt()
    {
        // Arrange: a source saved before the check, or with a stored setting the host later withdrew.
        var handler = Handler("tenant-a", resolver: null);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ResolveAsync(OwnSource(AzureAISearchSourceMetadata.ManagedIdentityAuthenticationType, "tenant-b-docs"), TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("host", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AConnectionWithoutAuthentication_SignsInAsTheHost_AndIsRefusedTheSameWay()
    {
        // Arrange: an HTTP API data connection with no authentication makes the client use the default Azure credential.
        var handler = Handler("tenant-a", new FakeResolver(AnonymousConnection()));
        var dataSource = ConnectionSource("tenant-b-docs");

        // Act
        var result = new ValidationResultDetails();
        await handler.ValidateAsync(dataSource, result, TestContext.Current.CancellationToken);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ResolveAsync(dataSource, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains(result.Errors, error => error.MemberNames.Contains(nameof(DataConnectionReference.ConnectionId)));
        Assert.Contains("host", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saving_AHostIdentitySource_OnTheDefaultTenant_NeedsNoApproval()
    {
        // Arrange: as for the SQL Server connector, the default tenant is the host's own.
        await using var services = Services(ShellSettings.DefaultShellName);

        // Act
        var result = await ValidateAsync(services, OwnSource(AzureAISearchSourceMetadata.ManagedIdentityAuthenticationType, "any-index"));

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.ErrorMessage)));
    }

    [Fact]
    public async Task Saving_AHostIdentitySource_OnATenant_ReadsOnlyTheServiceIndexAndIdentityTheHostApproved()
    {
        // Arrange: the operator approved one index for this tenant, with a user-assigned identity.
        var handler = Handler("tenant-a", resolver: null, Approved("tenant-a-docs", "11111111-1111-1111-1111-111111111111"));

        // Act
        var approved = await ValidateAsync(handler, OwnSource(AzureAISearchSourceMetadata.ManagedIdentityAuthenticationType, "tenant-a-docs", "11111111-1111-1111-1111-111111111111"));
        var otherIndex = await ValidateAsync(handler, OwnSource(AzureAISearchSourceMetadata.ManagedIdentityAuthenticationType, "tenant-b-docs", "11111111-1111-1111-1111-111111111111"));
        var otherIdentity = await ValidateAsync(handler, OwnSource(AzureAISearchSourceMetadata.ManagedIdentityAuthenticationType, "tenant-a-docs", identityClientId: null));
        var otherService = await ValidateAsync(handler, OwnSource(AzureAISearchSourceMetadata.ManagedIdentityAuthenticationType, "tenant-a-docs", "11111111-1111-1111-1111-111111111111", "https://other.search.windows.net"));
        var client = await handler.ResolveAsync(OwnSource(AzureAISearchSourceMetadata.ManagedIdentityAuthenticationType, "tenant-a-docs", "11111111-1111-1111-1111-111111111111"), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(approved.Succeeded, string.Join(" ", approved.Errors.Select(error => error.ErrorMessage)));
        Assert.False(otherIndex.Succeeded);
        Assert.False(otherIdentity.Succeeded);
        Assert.False(otherService.Succeeded);
        Assert.Equal("tenant-a-docs", client.IndexName);
    }

    [Fact]
    public async Task AConnectionWithoutAuthentication_ReadsAnApprovedIndex()
    {
        // Arrange
        var handler = Handler("tenant-a", new FakeResolver(AnonymousConnection()), Approved("tenant-a-docs"));
        var dataSource = ConnectionSource("tenant-a-docs");

        // Act
        var result = await ValidateAsync(handler, dataSource);
        var client = await handler.ResolveAsync(dataSource, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.ErrorMessage)));
        Assert.Equal(new Uri(Endpoint), client.Endpoint);
    }

    [Fact]
    public async Task Startup_BindsTheApprovals_FromTheTenantsConfiguration()
    {
        // Arrange: the tenant's configuration, which only the host's operator controls.
        var services = new ServiceCollection();
        services.AddSingleton<IShellConfiguration>(new ShellConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["CrestApps:AI:DataSources:AzureAISearch:HostIdentityIndexes:0:Endpoint"] = Endpoint,
            ["CrestApps:AI:DataSources:AzureAISearch:HostIdentityIndexes:0:IndexName"] = "tenant-a-docs",
        }).Build()));
        services.AddSingleton(new ShellSettings { Name = "tenant-a" });
        new CrestApps.OrchardCore.AI.DataSources.AzureAI.Startup(new PassThroughStringLocalizer<CrestApps.OrchardCore.AI.DataSources.AzureAI.Startup>()).ConfigureServices(services);
        await using var provider = services.BuildServiceProvider();

        // Act
        var hostIdentity = provider.GetRequiredService<AzureAISearchHostIdentity>();

        // Assert
        Assert.True(hostIdentity.IsApproved(Endpoint, "tenant-a-docs", identityClientId: null));
        Assert.False(hostIdentity.IsApproved(Endpoint, "tenant-b-docs", identityClientId: null));
    }

    private static async Task<ValidationResultDetails> ValidateAsync(AzureAISearchAIDataSourceSourceHandler handler, AIDataSource dataSource)
    {
        var result = new ValidationResultDetails();
        await handler.ValidateAsync(dataSource, result, TestContext.Current.CancellationToken);

        return result;
    }

    private static ServiceProvider Services(string tenant)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton<IAIEndpointPolicy>(new AIEndpointPolicy(Mock.Of<IHostEnvironment>(), Options.Create(new AIEgressOptions()), _ => []));
        services.AddSingleton(new ShellSettings { Name = tenant });
        services.AddSingleton(Options.Create(new AzureAISearchHostIdentityOptions()));
        services.AddSingleton<AzureAISearchHostIdentity>();
        services.AddAIDataSourceSourceHandler<AzureAISearchAIDataSourceSourceHandler>(AIDataSourceSourceTypes.AzureAISearch);
        services.AddScoped<ICatalogEntryHandler<AIDataSource>, AIDataSourceSourceValidationHandler>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static async Task<ValidationResultDetails> ValidateAsync(ServiceProvider services, AIDataSource dataSource)
    {
        await using var scope = services.CreateAsyncScope();
        var manager = new DefaultAIDataSourceManager(Mock.Of<IAIDataSourceStore>(), scope.ServiceProvider.GetServices<ICatalogEntryHandler<AIDataSource>>(), NullLogger<DefaultAIDataSourceManager>.Instance);

        return await manager.ValidateAsync(dataSource, TestContext.Current.CancellationToken);
    }

    private static AzureAISearchAIDataSourceSourceHandler Handler(string tenant, IDataConnectionResolver resolver, params AzureAISearchHostIdentityIndex[] approved)
        => new(
            new EphemeralDataProtectionProvider(),
            NullLogger<AzureAISearchAIDataSourceSourceHandler>.Instance,
            resolver,
            new AIEndpointPolicy(Mock.Of<IHostEnvironment>(), Options.Create(new AIEgressOptions()), _ => []),
            new AzureAISearchHostIdentity(new ShellSettings { Name = tenant }, Options.Create(new AzureAISearchHostIdentityOptions { HostIdentityIndexes = [.. approved] })));

    private static AzureAISearchHostIdentityIndex Approved(string indexName, string identityClientId = null)
        => new() { Endpoint = Endpoint + "/", IndexName = indexName, IdentityClientId = identityClientId };

    private static AIDataSource OwnSource(string authenticationType, string indexName, string identityClientId = null, string endpoint = Endpoint)
    {
        var dataSource = new AIDataSource { ItemId = "ds-1", Source = AIDataSourceSourceTypes.AzureAISearch, DisplayText = "Policies" };
        dataSource.Put(new AzureAISearchSourceMetadata
        {
            Endpoint = endpoint,
            AuthenticationType = authenticationType,
            IndexName = indexName,
            IdentityClientId = identityClientId,
        });

        return dataSource;
    }

    private static AIDataSource ConnectionSource(string indexName)
    {
        var dataSource = new AIDataSource { ItemId = "ds-2", Source = AIDataSourceSourceTypes.AzureAISearch, DisplayText = "Policies" };
        dataSource.Put(new AzureAISearchSourceMetadata { IndexName = indexName });
        dataSource.Put(new DataConnectionReference { ConnectionId = ConnectionId });

        return dataSource;
    }

    private static DataConnection AnonymousConnection()
    {
        var connection = new DataConnection { ItemId = ConnectionId, Name = "shared-search", Connector = DataConnectorConstants.Connectors.HttpApi };
        connection.Put(new HttpConnectionSettings
        {
            BaseUrl = Endpoint,
            Authentication = new HttpAuthenticationSettings { Type = HttpAuthenticationType.None },
        });

        return connection;
    }

    private sealed class FakeResolver : IDataConnectionResolver
    {
        private readonly DataConnection _connection;

        public FakeResolver(DataConnection connection)
        {
            _connection = connection;
        }

        public Task<IReadOnlyList<DataConnection>> ListAsync(IEnumerable<string> connectorNames, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DataConnection>>([_connection]);

        public Task<DataConnectorContext> ResolveAsync(string connectionId, CancellationToken cancellationToken = default)
            => Task.FromResult(_connection.ItemId == connectionId ? new DataConnectorContext(_connection, new FakeSecrets()) : null);
    }
}
