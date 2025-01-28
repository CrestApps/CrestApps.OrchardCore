using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Markdig;
using CrestApps.OrchardCore.OpenAI.Core.Models;
using CrestApps.OrchardCore.OpenAI.Core.Services;
using CrestApps.OrchardCore.OpenAI.Deployments.Drivers;
using CrestApps.OrchardCore.OpenAI.Deployments.Sources;
using CrestApps.OrchardCore.OpenAI.Deployments.Steps;
using CrestApps.OrchardCore.OpenAI.Drivers;
using CrestApps.OrchardCore.OpenAI.Endpoints;
using CrestApps.OrchardCore.OpenAI.Indexes;
using CrestApps.OrchardCore.OpenAI.Migrations;
using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.Recipes;
using CrestApps.OrchardCore.OpenAI.Services;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using CrestApps.OrchardCore.OpenAI.Workflows.Drivers;
using CrestApps.OrchardCore.OpenAI.Workflows.Models;
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

namespace CrestApps.OrchardCore.OpenAI;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<OpenAIChatProfile>();
            o.MemberAccessStrategy.Register<OpenAIChatSession>();
            o.MemberAccessStrategy.Register<OpenAIChatSessionPrompt>();
        });

        services
            .AddOpenAIDeploymentServices()
            .AddSingleton<IAIToolsService, DefaultAIToolsService>()
            .Configure<OpenAIMarkdownPipelineOptions>(options =>
            {
                options.MarkdownPipelineBuilder.Configure("advanced");
            })
            .AddScoped<IOpenAIMarkdownService, OpenAIMarkdownService>()
            .AddDisplayDriver<OpenAIDeployment, OpenAIDeploymentDisplayDriver>()
            .AddTransient<IConfigureOptions<OpenAIConnectionOptions>, OpenAIConnectionOptionsConfiguration>()
            .AddNavigationProvider<OpenAIAdminMenu>();
    }
}

[Feature(OpenAIConstants.Feature.ChatGPT)]
public sealed class ChatStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddOpenAIChatProfileServices()
            .AddScoped<IOpenAILinkGenerator, DefaultOpenAILinkGenerator>()
            .AddKeyedScoped<IOpenAIMarkdownService, OpenAIChatMarkdownService>("chat")
            .AddTransient<IConfigureOptions<ResourceManagementOptions>, ResourceManagementOptionsConfiguration>()
            .AddNavigationProvider<OpenAIChatAdminMenu>()
            .AddDataMigration<OpenAIChatSessionIndexMigrations>()
            .AddIndexProvider<OpenAIChatSessionIndexProvider>()
            .AddDisplayDriver<OpenAIChatProfile, OpenAIChatProfileDisplayDriver>()
            .AddDisplayDriver<OpenAIChatSession, OpenAIChatSessionDisplayDriver>()
            .AddDisplayDriver<OpenAIChatListOptions, OpenAIChatListOptionsDisplayDriver>()
            .Configure<OpenAIChatMarkdownPipelineOptions>(options =>
            {
                options.MarkdownPipelineBuilder
                .Configure("advanced")
                .Use<NewTabLinkExtension>();
            });

        services.AddDataMigration<OpenAIChatSettingsMigrations>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddOpenAIChatCompletionEndpoint<ChatStartup>()
            .AddOpenAIChatUtilityCompletionEndpoint<ChatStartup>()
            .AddOpenAIChatSessionEndpoint();
    }
}

[Feature(OpenAIConstants.Feature.Area)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<OpenAIDeploymentStep>();
    }
}

[Feature(OpenAIConstants.Feature.ChatGPT)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ChatRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<OpenAIChatProfileStep>();
    }
}

[Feature(OpenAIConstants.Feature.ChatGPT)]
[RequireFeatures("OrchardCore.Widgets")]
public sealed class WidgetsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddContentPart<OpenAIChatWidgetPart>()
            .UseDisplayDriver<OpenAIChatWidgetPartDisplayDriver>();

        services.AddDataMigration<WidgetsMigrations>();
    }
}

[Feature(OpenAIConstants.Feature.ChatGPT)]
[RequireFeatures("OrchardCore.Workflows")]
public sealed class ChatWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<OpenAIChatResponseMessage>();
        });
        services.AddActivity<ChatUtilityCompletionTask, ChatUtilityCompletionTaskDisplayDriver>();
    }
}

[Feature(OpenAIConstants.Feature.ChatGPT)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<OpenAIChatProfileDeploymentSource, OpenAIChatProfileDeploymentStep, OpenAIChatProfileDeploymentStepDisplayDriver>();
    }
}
