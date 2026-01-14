using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Drivers;
using CrestApps.OrchardCore.AI.Deployments.Sources;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Drivers;
using CrestApps.OrchardCore.AI.Endpoints;
using CrestApps.OrchardCore.AI.Endpoints.Api;
using CrestApps.OrchardCore.AI.Handlers;
using CrestApps.OrchardCore.AI.Indexes;
using CrestApps.OrchardCore.AI.Migrations;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Recipes;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.AI.Tools;
using CrestApps.OrchardCore.AI.Tools.Drivers;
using CrestApps.OrchardCore.AI.Workflows.Drivers;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.Services;
using Fluid;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.AI;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAICoreServices();
        services.AddPermissionProvider<AIPermissionsProvider>();
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<AIProfile>();
            o.MemberAccessStrategy.Register<AIChatSession>();
            o.MemberAccessStrategy.Register<AIChatSessionPrompt>();
        });

        services
            .AddScoped<IAILinkGenerator, DefaultAILinkGenerator>()
            .AddDisplayDriver<AIProfile, AIProfileDisplayDriver>()
            .AddTransient<IConfigureOptions<DefaultAIOptions>, DefaultAIOptionsConfiguration>()
            .AddNavigationProvider<AIProfileAdminMenu>();

        services
            .AddScoped<IAIToolsService, DefaultAIToolsService>()
            .AddTransient<IConfigureOptions<AIProviderOptions>, AIProviderOptionsConfiguration>();

        // Add tools core functionality.
        services
            .AddDisplayDriver<AIProfile, AIProfileToolsDisplayDriver>()
            .AddScoped<IAICompletionServiceHandler, FunctionInvocationAICompletionServiceHandler>();

#pragma warning disable CS0618 // Type or member is obsolete
        services.AddDataMigration<CatalogItemMigrations>();
#pragma warning restore CS0618 // Type or member is obsolete

        services.AddDataMigration<AIProfileDefaultContextMigrations>();

        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, ResourceManagementOptionsConfiguration>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddGetConnectionsEndpoint();
    }
}

[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIProfileStep>();
    }
}

[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<AIResponseMessage>();
        });

        services.AddActivity<AICompletionFromProfileTask, AICompletionFromProfileTaskDisplayDriver>();
        services.AddActivity<AICompletionWithConfigTask, AICompletionWithConfigTaskDisplayDriver>();
    }
}

[RequireFeatures("OrchardCore.Deployment")]
public sealed class OCDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIProfileDeploymentSource, AIProfileDeploymentStep, AIProfileDeploymentStepDisplayDriver>();
        services.AddDeployment<AIDeploymentDeploymentSource, AIDeploymentDeploymentStep, AIDeploymentDeploymentStepDisplayDriver>();
        services.AddDeployment<DeleteAIDeploymentDeploymentSource, DeleteAIDeploymentDeploymentStep, DeleteAIDeploymentDeploymentStepDisplayDriver>();
    }
}

# region Data Sources Feature

[Feature(AIConstants.Feature.DataSources)]
public sealed class DataSourceStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIDataSourceServices();
        services.AddScoped<IAICompletionContextBuilderHandler, DataSourceAICompletionContextBuilderHandler>();
        services.AddDisplayDriver<AIDataSource, AIDataSourceDisplayDriver>();
        services.AddPermissionProvider<AIDataSourcesPermissionProvider>();
        services.AddNavigationProvider<AIDataProviderAdminMenu>();
        services.AddDisplayDriver<AIProfile, AIProfileDataSourceDisplayDriver>();
    }
}

[Feature(AIConstants.Feature.DataSources)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class DataSourcesRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIDataSourceStep>();
    }
}

[RequireFeatures(AIConstants.Feature.DataSources, "OrchardCore.Deployment")]
public sealed class DataSourcesOCDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIDataSourceDeploymentSource, AIDataSourceDeploymentStep, AIDataSourceDeploymentStepDisplayDriver>();
    }
}
#endregion

#region Deployments Feature

[Feature(AIConstants.Feature.Deployments)]
public sealed class DeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddAIDeploymentServices()
            .AddPermissionProvider<AIDeploymentPermissionProvider>()
            .AddDisplayDriver<AIDeployment, AIDeploymentDisplayDriver>()
            .AddNavigationProvider<AIDeploymentAdminMenu>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddGetDeploymentsEndpoint();
    }
}

[Feature(AIConstants.Feature.Deployments)]
[RequireFeatures(AIConstants.Feature.ChatCore)]
public sealed class ChatDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddTransient<ICatalogEntryHandler<AIProfile>, AIDeploymentProfileHandler>()
            .AddDisplayDriver<AIProfile, AIProfileDeploymentDisplayDriver>();
    }
}

[Feature(AIConstants.Feature.Deployments)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class DeploymentRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIDeploymentStep>();
        services.AddRecipeExecutionStep<DeleteAIDeploymentStep>();
    }
}
#endregion

[Feature(AIConstants.Feature.ChatCore)]
public sealed class ChatCoreStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IAIChatSessionManager, DefaultAIChatSessionManager>()
            .AddDataMigration<AIChatSessionIndexMigrations>()
            .AddIndexProvider<AIChatSessionIndexProvider>();
    }
}

[Feature(AIConstants.Feature.ChatApi)]
public sealed class ApiChatStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddApiAIChatSessionEndpoint()
            .AddApiAIUtilityCompletionEndpoint<ApiChatStartup>()
            .AddApiAICompletionEndpoint<ApiChatStartup>();
    }
}

#region Tools Feature

[Feature(AIConstants.Feature.Tools)]
public sealed class ToolsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAICompletionContextBuilderHandler, ToolInstancesAICompletionContextBuilderHandler>();
        services.AddDisplayDriver<AIProfile, AIProfileToolInstancesDisplayDriver>();
        services.AddDisplayDriver<AIToolInstance, InvokableToolMetadataDisplayDriver>();
        services.AddDisplayDriver<AIToolInstance, AIProfileToolMetadataDisplayDriver>();
        services.AddDisplayDriver<AIToolInstance, AIToolInstanceDisplayDriver>();
        services.AddNavigationProvider<AIToolInstancesAdminMenu>();
        services.AddPermissionProvider<AIToolPermissionProvider>();

        services.AddAIToolSource<ProfileAwareAIToolSource>(ProfileAwareAIToolSource.ToolSource);
        services.AddScoped<IAICompletionServiceHandler, FunctionInstancesAICompletionServiceHandler>();
    }
}

[RequireFeatures(AIConstants.Feature.Tools, "OrchardCore.Recipes.Core")]
public sealed class RecipesToolsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIToolInstanceStep>();
    }
}

[RequireFeatures(AIConstants.Feature.Tools, "OrchardCore.Deployment")]
public sealed class ToolOCDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIToolInstanceDeploymentSource, AIToolInstanceDeploymentStep, AIToolInstanceDeploymentStepDisplayDriver>();
    }
}
#endregion

#region Connection Management Feature

[Feature(AIConstants.Feature.ConnectionManagement)]
public sealed class ConnectionManagementStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICatalogEntryHandler<AIProviderConnection>, AIProviderConnectionHandler>();
        services.AddTransient<IAIProviderConnectionHandler, SpeechToTextAIProviderConnectionHandler>();
        services.AddTransient<IConfigureOptions<AIProviderOptions>, AIProviderConnectionsOptionsConfiguration>();
        services.AddDisplayDriver<AIProviderConnection, AIProviderConnectionDisplayDriver>();
        services.AddNavigationProvider<AIConnectionsAdminMenu>();
        services.AddPermissionProvider<AIConnectionPermissionsProvider>();
    }
}

[Feature(AIConstants.Feature.ConnectionManagement)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ConnectionManagementRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIProviderConnectionsStep>();
    }
}

[Feature(AIConstants.Feature.ConnectionManagement)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ConnectionManagementOCDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIProviderConnectionDeploymentSource, AIProviderConnectionDeploymentStep, AIProviderConnectionDeploymentStepDisplayDriver>();
    }
}
#endregion
