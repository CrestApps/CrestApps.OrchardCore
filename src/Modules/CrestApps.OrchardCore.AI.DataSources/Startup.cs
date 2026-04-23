using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.BackgroundTasks;
using CrestApps.OrchardCore.AI.DataSources.Deployments;
using CrestApps.OrchardCore.AI.DataSources.Drivers;
using CrestApps.OrchardCore.AI.DataSources.Endpoints;
using CrestApps.OrchardCore.AI.DataSources.Handlers;
using CrestApps.OrchardCore.AI.DataSources.Migrations;
using CrestApps.OrchardCore.AI.DataSources.Recipes;
using CrestApps.OrchardCore.AI.DataSources.Services;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Indexing;
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
        services.AddCoreAIDataSourceRag();
        services.AddAIDataSourceServices();

        services.AddTransient<IConfigureOptions<AIDataSourceOptions>, AIDataSourceOptionsConfiguration>();
        services.AddDataMigration<DataSourceMetadataMigrations>();
        services.AddDisplayDriver<AIDataSource, AIDataSourceDisplayDriver>();
        services.AddPermissionProvider<AIDataSourcesPermissionProvider>();
        services.AddNavigationProvider<AIDataProviderAdminMenu>();
        services.AddDisplayDriver<AIProfile, AIProfileDataSourceDisplayDriver>();
        services.AddDisplayDriver<AIProfileTemplate, AIProfileTemplateDataSourceDisplayDriver>();
        services.AddDisplayDriver<IndexProfile, DataSourceIndexProfileDisplayDriver>();
        services
            .AddSiteDisplayDriver<AIDataSourceSettingsDisplayDriver>()
            .AddNavigationProvider<AISiteSettingsAdminMenu>();

        services.RemoveAll<IAIDataSourceIndexingQueue>()
            .AddScoped<IAIDataSourceIndexingQueue, OrchardAIDataSourceIndexingQueue>();

        services.RemoveAll<IAIDataSourceIndexingService>()
            .AddScoped<DataSourceIndexingService>()
            .AddScoped<IAIDataSourceIndexingService, OrchardAIDataSourceIndexingServiceAdapter>();

        services.AddSingleton<IBackgroundTask, DataSourceAlignmentBackgroundTask>();
        services.AddScoped<IDocumentIndexHandler, AIDataSourceDocumentIndexNotificationHandler>();
        services.AddIndexProfileHandler<DataSourceIndexProfileHandler>();
        services.AddIndexProfileHandler<DataSourceSourceIndexProfileHandler>();
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
