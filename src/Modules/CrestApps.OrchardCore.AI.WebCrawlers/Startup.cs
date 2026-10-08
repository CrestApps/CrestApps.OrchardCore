using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.Ingestion.Knowledge;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.WebCrawlers.BackgroundTasks;
using CrestApps.OrchardCore.AI.WebCrawlers.Drivers;
using CrestApps.OrchardCore.AI.WebCrawlers.Migrations;
using CrestApps.OrchardCore.AI.WebCrawlers.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.WebCrawlers;

/// <summary>
/// Registers services and configuration for the Web Crawlers feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Registers the Web AI data source source handler, the crawl strategies (sitemap), the re-index
        // planner and service, the crawler catalog handler, and the shared crawling primitives.
        services.AddCoreWebCrawlers()
            .AddCoreWebCrawlerStoresYesSql();

        // The framework also registers the shared ingestion run service here, for a crawler that feeds a
        // File data source. That service needs the per-item state store and knowledge ingestion, which only
        // File Sources and the Documents feature it depends on register. On a tenant without them the
        // container cannot build it, and because the re-index service takes it, every re-index throws.
        //
        // Built only when its dependencies are present, so the re-index service otherwise gets null and leaves
        // File-fed crawlers alone. Nothing is lost: only File Sources can create a File data source. Replace,
        // not TryAdd, so the outcome does not depend on which of the two features registers first.
        services.Replace(ServiceDescriptor.Scoped<IIngestionRunService>(sp =>
        {
            var isService = sp.GetRequiredService<IServiceProviderIsService>();

            return isService.IsService(typeof(IIngestionItemStateStore)) && isService.IsService(typeof(IKnowledgeIngestionService))
                ? ActivatorUtilities.CreateInstance<DefaultIngestionRunService>(sp)
                : null;
        }));

        services.AddDataMigration<WebCrawlerIndexMigrations>();

        services.AddDisplayDriver<WebCrawler, WebCrawlerDisplayDriver>();
        services.AddDisplayDriver<WebCrawler, SitemapWebCrawlerDisplayDriver>();
        services.AddDisplayDriver<AIDataSource, WebAIDataSourceDisplayDriver>();

        services.AddPermissionProvider<WebCrawlerPermissionProvider>();
        services.AddNavigationProvider<WebCrawlerAdminMenu>();

        services.AddSingleton<IBackgroundTask, WebCrawlerReindexBackgroundTask>();
    }
}
