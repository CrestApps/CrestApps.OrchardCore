using CrestApps.OrchardCore.AI.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.WebCrawlers.BackgroundTasks;
using CrestApps.OrchardCore.AI.WebCrawlers.Deployments;
using CrestApps.OrchardCore.AI.WebCrawlers.Drivers;
using CrestApps.OrchardCore.AI.WebCrawlers.Migrations;
using CrestApps.OrchardCore.AI.WebCrawlers.Recipes;
using CrestApps.OrchardCore.AI.WebCrawlers.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
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
            .AddCoreWebCrawlerStoresYesSql()
            .AddAIEgressGuards();

        services.AddDataMigration<WebCrawlerIndexMigrations>();

        services.AddDisplayDriver<WebCrawler, WebCrawlerDisplayDriver>();
        services.AddDisplayDriver<WebCrawler, SitemapWebCrawlerDisplayDriver>();
        services.AddDisplayDriver<AIDataSource, WebAIDataSourceDisplayDriver>();

        services.AddPermissionProvider<WebCrawlerPermissionProvider>();
        services.AddNavigationProvider<WebCrawlerAdminMenu>();

        services.AddSingleton<IBackgroundTask, WebCrawlerReindexBackgroundTask>();

        // Crawlers travel between sites without the secrets in their settings.
        services.AddRecipeExecutionStep<WebCrawlersStep>();
        services.AddScoped<IRecipeStep, WebCrawlersRecipeStepSchema>();
        services.AddDeployment<WebCrawlersDeploymentSource, WebCrawlersDeploymentStep, SummaryDeploymentStepDisplayDriver<WebCrawlersDeploymentStep>>();
        services.AddDeploymentStepSchema<WebCrawlersDeploymentStepSchema>();
    }
}
