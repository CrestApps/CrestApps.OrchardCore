using System.Net;
using System.Net.Sockets;
using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Models;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.Tests.DataConnectors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

/// <summary>
/// C14 (review 11): the AI search endpoints were checked only when they were saved, and a name that did not resolve
/// passed. A name saved that way could later be pointed at the site's own network, and the search client then called
/// inside with the API key. The endpoint policy now refuses a name it cannot resolve, and the Azure AI Search and
/// Elasticsearch clients the data sources build connect through the egress guard, so the address is checked when it is
/// connected to. These tests let the save-time policy pass everything, as a name that answers differently later does,
/// and point the client at this machine.
/// </summary>
public sealed class SearchEndpointConnectTimeEgressTests
{
    private const string ConnectionId = "4qkc7h2m9x1r5t8v3w6y0z2b4d";

    private readonly EphemeralDataProtectionProvider _protection = new();

    [Fact]
    public void EndpointPolicy_ANameThatDoesNotResolve_IsRefused()
    {
        var policy = new AIEndpointPolicy(Mock.Of<IHostEnvironment>(), Options.Create(new AIEgressOptions()), _ => throw new SocketException((int)SocketError.HostNotFound));

        Assert.False(policy.IsAllowed("https://not-yet.example.com/"));
    }

    [Fact]
    public void EndpointPolicy_ANameWithNoAddress_IsRefused()
    {
        var policy = new AIEndpointPolicy(Mock.Of<IHostEnvironment>(), Options.Create(new AIEgressOptions()), _ => []);

        Assert.False(policy.IsAllowed("https://not-yet.example.com/"));
    }

    [Fact]
    public void EndpointPolicy_ANameThatDoesNotResolve_IsAllowed_WhereTheHostAllowsAPrivateNetwork()
    {
        // A private resolver may answer the name only when it is called; the host's operator opted in.
        var policy = new AIEndpointPolicy(Mock.Of<IHostEnvironment>(), Options.Create(new AIEgressOptions { AllowPrivateNetwork = true }), _ => throw new SocketException((int)SocketError.HostNotFound));

        Assert.True(policy.IsAllowed("http://ollama.internal:11434/"));
    }

    [Fact]
    public async Task AzureAISearch_AnEndpointOnTheSiteOwnNetwork_IsRefusedWhenItIsConnectedTo()
    {
        using var server = new CountingServer();
        var handler = Create<AzureAISearchAIDataSourceSourceHandler>(services => services.AddSingleton(new AIEgressTransport(Options.Create(new AIEgressOptions()))));
        var dataSource = new AIDataSource { ItemId = "ds-1", Source = AIDataSourceSourceTypes.AzureAISearch, DisplayText = "Policies" };
        dataSource.Put(new AzureAISearchSourceMetadata
        {
            Endpoint = $"https://127.0.0.1:{server.Port}",
            AuthenticationType = AzureAISearchSourceMetadata.ApiKeyAuthenticationType,
            ApiKey = _protection.CreateProtector(AIDataSourceProtectionConstants.SourceSecretPurpose).Protect("query-key"),
            IndexName = "docs",
        });

        var client = await handler.ResolveAsync(dataSource, TestContext.Current.CancellationToken);
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => client.GetDocumentCountAsync(TestContext.Current.CancellationToken));

        Assert.True(Mentions(exception, EgressNetwork.RefusedMessage), exception.ToString());
        Assert.Equal(0, server.Accepted);
    }

    [Fact]
    public async Task AzureAISearch_ADataConnectionOnTheSiteOwnNetwork_IsRefusedWhenItIsConnectedTo()
    {
        using var server = new CountingServer();
        var connection = new DataConnection { ItemId = ConnectionId, Name = "search", Connector = DataConnectorConstants.Connectors.HttpApi };
        connection.Put(new HttpConnectionSettings
        {
            BaseUrl = $"https://127.0.0.1:{server.Port}",
            Authentication = new HttpAuthenticationSettings { Type = HttpAuthenticationType.ApiKey, ApiKeyName = "api-key" },
        });

        var handler = Create<AzureAISearchAIDataSourceSourceHandler>(services => services.AddSingleton<IDataConnectionResolver>(new SingleConnectionResolver(connection, HttpAuthenticationSecrets.ApiKey, "query-key")));
        var dataSource = new AIDataSource { ItemId = "ds-2", Source = AIDataSourceSourceTypes.AzureAISearch, DisplayText = "Policies" };
        dataSource.Put(new AzureAISearchSourceMetadata { IndexName = "docs" });
        dataSource.Put(new DataConnectionReference { ConnectionId = ConnectionId });

        var client = await handler.ResolveAsync(dataSource, TestContext.Current.CancellationToken);
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => client.GetDocumentCountAsync(TestContext.Current.CancellationToken));

        Assert.True(Mentions(exception, "site's own network"), exception.ToString());
        Assert.Equal(0, server.Accepted);
    }

    [Fact]
    public async Task Elasticsearch_ASelfManagedClusterAtTheMetadataService_IsRefusedWhenItIsConnectedTo()
    {
        // A self-managed cluster may stay on a private network (review 8, G7), but never at the cloud metadata service.
        var handler = Create<ElasticsearchAIDataSourceSourceHandler>(services => services.AddSingleton(new AIEgressTransport(Options.Create(new AIEgressOptions()))));
        var dataSource = new AIDataSource { ItemId = "ds-3", Source = AIDataSourceSourceTypes.Elasticsearch, DisplayText = "Logs" };
        dataSource.Put(new ElasticsearchSourceMetadata
        {
            EnvironmentType = ElasticsearchSourceMetadata.SelfManagedEnvironmentType,
            Url = "http://169.254.169.254:9200",
            AuthenticationType = ElasticsearchSourceMetadata.NoneAuthenticationType,
            IndexName = "logs",
        });

        var (client, _) = await handler.ResolveAsync(dataSource, TestContext.Current.CancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        var response = await client.PingAsync(timeout.Token);

        Assert.False(response.IsValidResponse);
        Assert.True(Mentions(response.ApiCallDetails.OriginalException, EgressNetwork.RefusedMessage), response.ApiCallDetails.OriginalException?.ToString());
    }

    [Fact]
    public async Task Elasticsearch_ADataConnectionOnTheSiteOwnNetwork_IsRefusedWhenItIsConnectedTo()
    {
        using var server = new CountingServer();
        var connection = new DataConnection { ItemId = ConnectionId, Name = "logs", Connector = DataConnectorConstants.Connectors.Elasticsearch };
        connection.Put(new HttpConnectionSettings
        {
            BaseUrl = $"http://127.0.0.1:{server.Port}",
            Authentication = new HttpAuthenticationSettings { Type = HttpAuthenticationType.None },
        });

        var handler = Create<ElasticsearchAIDataSourceSourceHandler>(services => services.AddSingleton<IDataConnectionResolver>(new SingleConnectionResolver(connection)));
        var dataSource = new AIDataSource { ItemId = "ds-4", Source = AIDataSourceSourceTypes.Elasticsearch, DisplayText = "Logs" };
        dataSource.Put(new ElasticsearchSourceMetadata { IndexName = "logs" });
        dataSource.Put(new DataConnectionReference { ConnectionId = ConnectionId });

        var (client, _) = await handler.ResolveAsync(dataSource, TestContext.Current.CancellationToken);
        var response = await client.PingAsync(TestContext.Current.CancellationToken);

        Assert.False(response.IsValidResponse);
        Assert.True(Mentions(response.ApiCallDetails.OriginalException, "site's own network"), response.ApiCallDetails.OriginalException?.ToString());
        Assert.Equal(0, server.Accepted);
    }

    // The handler as the container builds it: the save-time policy allows every endpoint, and the data connectors'
    // shared client is registered as the Data Connectors feature registers it, with the default network rule.
    private TSourceHandler Create<TSourceHandler>(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton<IDataProtectionProvider>(_protection);
        services.AddSingleton(Options.Create(new ElasticsearchDataSourceOptions()));
        services.AddSingleton(new ShellSettings { Name = ShellSettings.DefaultShellName });
        services.AddSingleton(Mock.Of<IAIEndpointPolicy>(policy => policy.IsAllowed(It.IsAny<string>())));
        services.AddDataConnectorsCore();
        services.AddSingleton<IDataConnectionResolver>(new SingleConnectionResolver(connection: null));
        configure(services);

        return ActivatorUtilities.CreateInstance<TSourceHandler>(services.BuildServiceProvider());
    }

    private static bool Mentions(Exception exception, string text)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(text, StringComparison.Ordinal))
            {
                return true;
            }

            if (current is AggregateException aggregate && aggregate.InnerExceptions.Any(inner => Mentions(inner, text)))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class SingleConnectionResolver : IDataConnectionResolver
    {
        private readonly DataConnection _connection;
        private readonly Dictionary<string, string> _secrets;

        public SingleConnectionResolver(DataConnection connection, string secretName = null, string secretValue = null)
        {
            _connection = connection;
            _secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (secretName is not null)
            {
                _secrets[secretName] = secretValue;
            }
        }

        public Task<IReadOnlyList<DataConnection>> ListAsync(IEnumerable<string> connectorNames, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DataConnection>>(_connection is null ? [] : [_connection]);

        public Task<DataConnectorContext> ResolveAsync(string connectionId, CancellationToken cancellationToken = default)
            => Task.FromResult(_connection?.ItemId == connectionId ? new DataConnectorContext(_connection, new FakeSecrets(_secrets)) : null);
    }

    // Counts the connections made to it on this machine, and closes each.
    private sealed class CountingServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private int _accepted;

        public CountingServer()
        {
            _listener.Start();
            _ = Task.Run(AcceptAsync, CancellationToken.None);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public int Accepted => Volatile.Read(ref _accepted);

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
            _stop.Dispose();
        }

        private async Task AcceptAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    Interlocked.Increment(ref _accepted);
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
                {
                    return;
                }
            }
        }
    }
}
