using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.DataSources.WebCrawlers.BackgroundTasks;
using CrestApps.OrchardCore.AI.DataSources.WebCrawlers.Drivers;
using CrestApps.OrchardCore.AI.DataSources.WebCrawlers.Migrations;
using CrestApps.OrchardCore.AI.DataSources.WebCrawlers.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.DataSources.WebCrawlers;

/// <summary>
/// Registers services and configuration for the Web Crawlers feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Registers the Web AI data source source handler, the crawl strategies (sitemap), the re-index
        // planner and service, the crawler catalog handler, and the shared crawling primitives.
        services.AddCoreWebCrawlers();

        // Registers the YesSql-backed IWebCrawlerStore and IWebCrawlStateStore plus their index providers.
        // The crawler indexes live in the AI collection, which the AI feature (a dependency) initializes.
        services.AddCoreWebCrawlerStoresYesSql();

        services.AddDataMigration<WebCrawlerIndexMigrations>();

        services.AddDisplayDriver<WebCrawler, WebCrawlerDisplayDriver>();
        services.AddDisplayDriver<WebCrawler, SitemapWebCrawlerDisplayDriver>();
        services.AddDisplayDriver<AIDataSource, WebAIDataSourceDisplayDriver>();

        services.AddPermissionProvider<WebCrawlerPermissionProvider>();
        services.AddNavigationProvider<WebCrawlerAdminMenu>();

        services.AddSingleton<IBackgroundTask, WebCrawlerReindexBackgroundTask>();
    }
}
