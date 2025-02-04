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
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<AIChatProfile>();
            o.MemberAccessStrategy.Register<AIChatSession>();
            o.MemberAccessStrategy.Register<AIChatSessionPrompt>();
        });

        services
            .AddSingleton<IAIToolsService, DefaultAIToolsService>()
            .Configure<AIMarkdownPipelineOptions>(options =>
            {
                options.MarkdownPipelineBuilder.Configure("advanced");
            })
            .AddScoped<IAIMarkdownService, AIMarkdownService>()
            .AddTransient<IConfigureOptions<AIProviderOptions>, AIProviderOptionsConfiguration>();
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
            .AddTransient<IAIChatProfileHandler, AIDeploymentChatProfileHandler>()
            .AddDisplayDriver<AIChatProfile, AIChatProfileDeploymentDisplayDriver>();
    }
}

[Feature(AIConstants.Feature.Chat)]
public sealed class ChatStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddAIChatProfileServices()
            .AddScoped<IAILinkGenerator, DefaultAILinkGenerator>()
            .AddKeyedScoped<IAIMarkdownService, AIChatMarkdownService>("chat")
            .AddTransient<IConfigureOptions<ResourceManagementOptions>, ResourceManagementOptionsConfiguration>()
            .AddNavigationProvider<AIChatAdminMenu>()
            .AddDataMigration<AIChatSessionIndexMigrations>()
            .AddIndexProvider<AIChatSessionIndexProvider>()
            .AddDisplayDriver<AIChatProfile, AIChatProfileDisplayDriver>()
            .AddDisplayDriver<AIChatSession, AIChatSessionDisplayDriver>()
            .AddDisplayDriver<AIChatListOptions, AIChatListOptionsDisplayDriver>()
            .Configure<AIChatMarkdownPipelineOptions>(options =>
            {
                options.MarkdownPipelineBuilder
                .Configure("advanced")
                .Use<NewTabLinkExtension>();
            });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddAIChatCompletionEndpoint<ChatStartup>()
            .AddAIChatUtilityCompletionEndpoint<ChatStartup>()
            .AddAIChatSessionEndpoint();
    }
}

[Feature(AIConstants.Feature.Area)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIDeploymentStep>();
    }
}

[Feature(AIConstants.Feature.Chat)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ChatRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIChatProfileStep>();
    }
}

[Feature(AIConstants.Feature.Chat)]
[RequireFeatures("OrchardCore.Widgets")]
public sealed class WidgetsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddContentPart<AIChatProfilePart>()
            .UseDisplayDriver<AIChatProfilePartDisplayDriver>();

        services.AddDataMigration<AIChatMigrations>();
    }
}

[Feature(AIConstants.Feature.Chat)]
[RequireFeatures("OrchardCore.Workflows")]
public sealed class ChatWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<AIChatResponseMessage>();
        });
        services.AddActivity<ChatUtilityCompletionTask, ChatUtilityCompletionTaskDisplayDriver>();
    }
}

[Feature(AIConstants.Feature.Chat)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ChatOCDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIChatProfileDeploymentSource, AIChatProfileDeploymentStep, AIChatProfileDeploymentStepDisplayDriver>();
    }
}

[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIDeploymentDeploymentSource, AIDeploymentDeploymentStep, AIDeploymentDeploymentStepDisplayDriver>();
    }
}
