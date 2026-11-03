using CrestApps.OrchardCore.DataConnectors.BackgroundTasks;
using CrestApps.OrchardCore.DataConnectors.Connectors.Elasticsearch;
using CrestApps.OrchardCore.DataConnectors.Connectors.Files;
using CrestApps.OrchardCore.DataConnectors.Connectors.Http;
using CrestApps.OrchardCore.DataConnectors.Deployments;
using CrestApps.OrchardCore.DataConnectors.Drivers;
using CrestApps.OrchardCore.DataConnectors.Indexes;
using CrestApps.OrchardCore.DataConnectors.Migrations;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Recipes;
using CrestApps.OrchardCore.DataConnectors.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// Registers connections, connected data sets, their live and imported data sources, the refresh task and the admin
/// screens.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The tenant's configuration.</param>
    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        // Connections, data sets and refreshes live in their own YesSql collection, which has to be declared to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(DataConnectorConstants.CollectionName));
        services.AddDataMigration<DataConnectorsMigrations>()
            .AddIndexProvider<DataConnectionIndexProvider>()
            .AddIndexProvider<ConnectedDataSetIndexProvider>()
            .AddIndexProvider<DataSetRefreshRunIndexProvider>();

        services.AddDataConnectorsCore();
        services.AddScoped<DataSetConnectionResolver>();
        services.Configure<DataConnectorsOptions>(_shellConfiguration.GetSection("CrestApps:DataConnectors"));
        services.Configure<DataConnectorNetworkOptions>(_shellConfiguration.GetSection("CrestApps:DataConnectors:Network"));

        services.AddPermissionProvider<DataConnectorsPermissionProvider>();
        services.AddNavigationProvider<DataConnectorsAdminMenu>();

        services.AddDisplayDriver<DataConnection, DataConnectionDisplayDriver>();
        services.AddDisplayDriver<DataConnection, HttpConnectionDisplayDriver>();
        services.AddDisplayDriver<ConnectedDataSet, ConnectedDataSetDisplayDriver>();
        services.AddDisplayDriver<ConnectedDataSet, RelationalDataSetDisplayDriver>();
        services.AddDisplayDriver<DataSetRefreshRun, DataSetRefreshRunDisplayDriver>();

        services.AddSingleton<IBackgroundTask, DataSetRefreshBackgroundTask>();
    }
}

/// <summary>
/// Registers the HTTP API connector.
/// </summary>
[Feature(DataConnectorConstants.Features.Http)]
public sealed class HttpStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataConnector<HttpApiDataConnector>();
        services.AddDisplayDriver<ConnectedDataSet, HttpApiDataSetDisplayDriver>();
    }
}

/// <summary>
/// Registers the Elasticsearch connector.
/// </summary>
[Feature(DataConnectorConstants.Features.Elasticsearch)]
public sealed class ElasticsearchStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataConnector<ElasticsearchDataConnector>();
        services.AddDisplayDriver<ConnectedDataSet, ElasticsearchDataSetDisplayDriver>();
    }
}

/// <summary>
/// Registers the media files connector.
/// </summary>
[Feature(DataConnectorConstants.Features.Files)]
public sealed class FilesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataConnector<MediaFilesDataConnector>();
        services.AddDisplayDriver<DataConnection, MediaFilesConnectionDisplayDriver>();
        services.AddDisplayDriver<ConnectedDataSet, MediaFileDataSetDisplayDriver>();
    }
}

/// <summary>
/// Registers the recipe steps that import connections and data sets.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<DataConnectionStep>();
        services.AddRecipeExecutionStep<ConnectedDataSetStep>();
    }
}

/// <summary>
/// Registers the deployment steps that export connections and data sets.
/// </summary>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<DataConnectionDeploymentSource, DataConnectionDeploymentStep, DataConnectionDeploymentStepDisplayDriver>();
        services.AddDeployment<ConnectedDataSetDeploymentSource, ConnectedDataSetDeploymentStep, ConnectedDataSetDeploymentStepDisplayDriver>();
    }
}
