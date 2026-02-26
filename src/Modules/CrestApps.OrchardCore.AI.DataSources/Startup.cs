using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.BackgroundTasks;
using CrestApps.OrchardCore.AI.DataSources.Deployments;
using CrestApps.OrchardCore.AI.DataSources.Drivers;
using CrestApps.OrchardCore.AI.DataSources.Endpoints;
using CrestApps.OrchardCore.AI.DataSources.Handlers;
using CrestApps.OrchardCore.AI.DataSources.Recipes;
using CrestApps.OrchardCore.AI.DataSources.Services;
using CrestApps.OrchardCore.AI.DataSources.Tools;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.DataSources;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIDataSourceServices();
        services.AddScoped<IAICompletionContextBuilderHandler, DataSourceAICompletionContextBuilderHandler>();
        services.AddDisplayDriver<AIDataSource, AIDataSourceDisplayDriver>();
        services.AddPermissionProvider<AIDataSourcesPermissionProvider>();
        services.AddNavigationProvider<AIDataProviderAdminMenu>();
        services.AddDisplayDriver<AIProfile, AIProfileDataSourceDisplayDriver>();
        services.AddDisplayDriver<IndexProfile, DataSourceIndexProfileDisplayDriver>();
        services
            .AddSiteDisplayDriver<AIDataSourceSettingsDisplayDriver>()
            .AddNavigationProvider<AISiteSettingsAdminMenu>();

        services.AddScoped<IPreemptiveRagHandler, DataSourcePreemptiveRagHandler>();

        services.AddScoped<DataSourceIndexingService>();
        services.AddIndexProfileHandler<DataSourceIndexProfileHandler>();
        services.AddSingleton<IBackgroundTask, DataSourceSyncBackgroundTask>();
        services.AddSingleton<IBackgroundTask, DataSourceAlignmentBackgroundTask>();
        services.AddTransient<ICatalogEntryHandler<AIDataSource>, DataSourceIndexingHandler>();

        services.AddAITool<DataSourceSearchTool>(DataSourceSearchTool.TheName)
            .WithPurpose(AIToolPurposes.DataSourceSearch);
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddGetDataSourceFieldsEndpoint();
    }
}

[RequireFeatures("OrchardCore.Contents")]
public sealed class DataSourcesContentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentHandler, DataSourceContentHandler>();
    }
}

[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class DataSourcesRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIDataSourceStep>();
    }
}

[RequireFeatures("OrchardCore.Deployment")]
public sealed class DataSourcesOCDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIDataSourceDeploymentSource, AIDataSourceDeploymentStep, AIDataSourceDeploymentStepDisplayDriver>();
    }
}
