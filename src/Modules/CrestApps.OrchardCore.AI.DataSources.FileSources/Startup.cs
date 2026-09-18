using CrestApps.Core.AI.Documents.Endpoints;
using CrestApps.Core.AI.FileSources;
using CrestApps.Core.AI.FileSources.Connectors;
using CrestApps.Core.AI.Indexing;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.DataSources.FileSources.BackgroundTasks;
using CrestApps.OrchardCore.AI.DataSources.FileSources.Drivers;
using CrestApps.OrchardCore.AI.DataSources.FileSources.Handlers;
using CrestApps.OrchardCore.AI.DataSources.FileSources.Migrations;
using CrestApps.OrchardCore.AI.DataSources.FileSources.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.DataSources.FileSources;

/// <summary>
/// Registers services and configuration for the File Sources feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Registers the File AI data source handler, the connector resolver, the run service and the
        // knowledge ingestion the connectors feed. Nothing here grants access to a folder on its own.
        services.AddCoreFileSources();
        services.AddCoreFileSystemConnector();

        // The framework's file-system connector, wrapped so it can only ever read inside this tenant's own
        // folder. Registered after the framework's, because a keyed service resolves to the last
        // registration -- which is how the framework itself replaces a reader for a shared media type.
        services.AddScoped<TenantFileSystemIngestionConnector>();
        services.AddKeyedScoped<IIngestionConnector>(
            FileSystemIngestionConnector.ConnectorName,
            (sp, _) => sp.GetRequiredService<TenantFileSystemIngestionConnector>());

        // Singleton, not scoped: the folder is a pure function of the tenant's own identity and never
        // changes for the life of the shell, and the options it feeds are themselves a singleton. A scoped
        // registration here is a captive dependency, which the container refuses to build at all.
        services.AddSingleton<ITenantFileSourceRoot, TenantFileSourceRoot>();
        services.AddTransient<IConfigureOptions<FileSourceOptions>, FileSourceOptionsConfiguration>();

        // Registers the YesSql-backed IWebCrawlerStore (which also stores file sources) and the knowledge
        // object store the ingested pieces land in.
        services.AddCoreWebCrawlerStoresYesSql();

        services.AddDataMigration<FileSourceIndexMigrations>();

        services.AddDisplayDriver<WebCrawler, FileSourceDisplayDriver>();
        services.AddDisplayDriver<WebCrawler, FileSystemFileSourceDisplayDriver>();
        services.AddDisplayDriver<AIDataSource, FileAIDataSourceDisplayDriver>();

        services.AddScoped<IAuthorizationHandler, OrchardKnowledgeFigureAuthorizationHandler>();

        services.AddPermissionProvider<FileSourcePermissionProvider>();
        services.AddNavigationProvider<FileSourceAdminMenu>();

        services.AddSingleton<IBackgroundTask, FileSourceRunBackgroundTask>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // An answer citing a figure or a chart from an ingested file links here. Mapped by this feature
        // because it is this feature that registers the knowledge store the endpoint reads.
        routes.AddDownloadKnowledgeFigureEndpoint();
    }
}
