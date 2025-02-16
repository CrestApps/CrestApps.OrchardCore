using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Markdig;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Drivers;
using CrestApps.OrchardCore.AI.Deployments.Sources;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Drivers;
using CrestApps.OrchardCore.AI.Endpoints;
using CrestApps.OrchardCore.AI.Endpoints.Api;
using CrestApps.OrchardCore.AI.Hubs;
using CrestApps.OrchardCore.AI.Indexes;
using CrestApps.OrchardCore.AI.Migrations;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Recipes;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.AI.Workflows.Drivers;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Fluid;
using Markdig;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.ResourceManagement;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.AI;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIProfileServices();
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<AIProfile>();
            o.MemberAccessStrategy.Register<AIChatSession>();
            o.MemberAccessStrategy.Register<AIChatSessionPrompt>();
        });

        services
            .AddScoped<IAILinkGenerator, DefaultAILinkGenerator>()
            .AddDisplayDriver<AIProfile, AIProfileDisplayDriver>()
            .AddDisplayDriver<AIChatListOptions, AIProfileOptionsDisplayDriver>()
            .AddTransient<IConfigureOptions<DefaultAIOptions>, DefaultAIOptionsConfiguration>()
            .AddNavigationProvider<AIProfileAdminMenu>();

        services
            .AddSingleton<IAIToolsService, DefaultAIToolsService>()
            .Configure<AIMarkdownPipelineOptions>(options =>
            {
                options.MarkdownPipelineBuilder.Configure("advanced");
            })
            .AddScoped<IAIMarkdownService, AIMarkdownService>()
            .AddTransient<IConfigureOptions<AIProviderOptions>, AIProviderOptionsConfiguration>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddAICompletionEndpoint<ChatStartup>()
            .AddAIUtilityCompletionEndpoint<ChatStartup>();
    }
}

[Feature(AIConstants.Feature.Deployments)]
public sealed class DeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddAIDeploymentServices()
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
[RequireFeatures(AIConstants.Feature.Chat)]
public sealed class ChatDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddTransient<IAIProfileHandler, AIDeploymentProfileHandler>()
            .AddDisplayDriver<AIProfile, AIProfileDeploymentDisplayDriver>();
    }
}

[Feature(AIConstants.Feature.Chat)]
public sealed class ChatStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddKeyedScoped<IAIMarkdownService, AIChatMarkdownService>("chat")
            .AddTransient<IConfigureOptions<ResourceManagementOptions>, ResourceManagementOptionsConfiguration>()
            .AddDataMigration<AIChatSessionIndexMigrations>()
            .AddIndexProvider<AIChatSessionIndexProvider>()
            .AddDisplayDriver<AIChatSession, AIChatSessionDisplayDriver>()
            .Configure<AIChatMarkdownPipelineOptions>(options =>
            {
                options.MarkdownPipelineBuilder
                .Configure("advanced")
                .Use<NewTabLinkExtension>();
            });

        services
            .AddSingleton<IConfigureOptions<HubOptions<ChatHub>>, HubOptionsSetup<ChatHub>>();

        services
            .AddNavigationProvider<ChatAdminMenu>();

    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapHub<ChatHub>(AIConstants.Hubs.ChatHub);

        routes
            .AddAIChatSessionEndpoint();
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
            .AddApiAICompletionEndpoint<ChatStartup>();
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
        services.AddActivity<AICompletionTask, AICompletionTaskDisplayDriver>();
    }
}

[RequireFeatures("OrchardCore.Deployment")]
public sealed class OCDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIProfileDeploymentSource, AIProfileDeploymentStep, AIProfileDeploymentStepDisplayDriver>();
        services.AddDeployment<AIDeploymentDeploymentSource, AIDeploymentDeploymentStep, AIDeploymentDeploymentStepDisplayDriver>();
    }
}

[Feature(AIConstants.Feature.Chat)]
[RequireFeatures("OrchardCore.Widgets")]
public sealed class WidgetsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddContentPart<AIProfilePart>()
            .UseDisplayDriver<AIChatProfilePartDisplayDriver>();

        services.AddDataMigration<AIChatMigrations>();
    }
}

[Feature(AIConstants.Feature.Deployments)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class DeploymentRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIDeploymentStep>();
    }
}
