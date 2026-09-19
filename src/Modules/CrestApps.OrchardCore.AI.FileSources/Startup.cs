using CrestApps.Core.AI.Documents.Endpoints;
using CrestApps.Core.AI.FileSources;
using CrestApps.Core.AI.FileSources.Connectors;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.FileSources.BackgroundTasks;
using CrestApps.OrchardCore.AI.FileSources.Drivers;
using CrestApps.OrchardCore.AI.FileSources.Handlers;
using CrestApps.OrchardCore.AI.FileSources.Migrations;
using CrestApps.OrchardCore.AI.FileSources.Services;
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

namespace CrestApps.OrchardCore.AI.FileSources;

/// <summary>
/// Registers services and configuration for the File Sources feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Registers the File AI data source handler, the connector resolver, the run service, the scheduler
        // and the knowledge ingestion the connectors feed. Nothing here grants access to a folder on its own.
        services.AddCoreFileSources()
            .AddCoreFileSystemConnector();

        // The one folder this tenant may read. It is computed from the tenant's own shell settings and
        // never from configuration or a request, so a tenant administrator cannot widen their own reach.
        //
        // Singleton, not scoped: the folder is a pure function of the tenant's identity and never changes
        // for the life of the shell, and the options it feeds are themselves a singleton. A scoped
        // registration here is a captive dependency, which the container refuses to build at all.
        services.AddSingleton<ITenantFileSourceRoot, TenantFileSourceRoot>();

        services.AddTransient<IConfigureOptions<FileSourceOptions>, FileSourceOptionsConfiguration>();

        // Post-configure, not configure: the framework's own configuration step adds whatever the host set
        // and points the base path at the content root. This has to run after it to take both back, and a
        // single surviving entry is the whole tenant boundary gone.
        services.AddTransient<IPostConfigureOptions<FileSystemConnectorOptions>, FileSystemConnectorOptionsConfiguration>();

        // The file source records, their per-item ingestion state, and their index providers.
        services.AddCoreFileSourceStoresYesSql();

        // Still needed for two things the file source feature cannot get anywhere else in
        // CrestApps.Core 2.0.0-preview.284: IKnowledgeObjectStore, which only this registration provides,
        // and IWebCrawlerStore, which DefaultFileSourceScheduler takes as a required dependency even in a
        // host that has no web crawlers. Both are Core-side gaps this split exposed; when Core offers a
        // knowledge-store registration of its own and makes the scheduler's crawler store optional, this
        // call and the crawler tables in FileSourceIndexMigrations go together.
        services.AddCoreWebCrawlerStoresYesSql();

        services.AddDataMigration<FileSourceIndexMigrations>();

        services.AddDisplayDriver<FileSource, FileSourceDisplayDriver>();
        services.AddDisplayDriver<FileSource, FileSystemFileSourceDisplayDriver>();
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
