using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.DataSources;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.DataConnectors.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers data connectors.
/// </summary>
public static class DataConnectorsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared services of data connectors: the connector registry, the stores, the row store, the
    /// refresh engine, the HTTP client HTTP-based connectors share, and the live and imported data sources.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddDataConnectorsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IDataConnectorManager, DataConnectorManager>();
        services.TryAddScoped<IDataConnectionSecretManager, DataConnectionSecretManager>();
        services.TryAddScoped<IDataConnectionStore, DataConnectionStore>();
        services.TryAddScoped<IConnectedDataSetStore, ConnectedDataSetStore>();
        services.TryAddScoped<IDataSetRefreshRunStore, DataSetRefreshRunStore>();
        services.TryAddScoped<IDataSetRowStore, SqlDataSetRowStore>();
        services.TryAddScoped<IDataSetRefreshService, DataSetRefreshService>();
        services.TryAddScoped<ConnectedDataReader>();
        services.TryAddScoped<ConnectedDataSetAuthorizer>();

        services.TryAddSingleton<HttpRequestAuthenticator>();
        services.TryAddScoped<DataConnectorHttpClient>();

        // Redirects are followed by the client itself, only to the same origin; the handler refuses to connect to the
        // site's own network unless the configuration allows it, whatever a host name resolves to when it is called.
        services.AddHttpClient(DataConnectorConstants.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(serviceProvider => DataConnectorNetwork.CreateHandler(serviceProvider.GetRequiredService<IOptions<DataConnectorNetworkOptions>>().Value));

        services.AddDataSource<ConnectedDataSource>();
        services.AddDataSource<ImportedDataSource>();

        return services;
    }

    /// <summary>
    /// Registers a data connector, so connections can use it.
    /// </summary>
    /// <typeparam name="TConnector">The connector type.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddDataConnector<TConnector>(this IServiceCollection services)
        where TConnector : class, IDataConnector
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<TConnector>();
        services.Configure<DataConnectorOptions>(options => options.Add(DataConnectorDescriptor.Create<TConnector>()));

        return services;
    }
}
