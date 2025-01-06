using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Functions;
using CrestApps.OrchardCore.OpenAI.Core.Models;
using CrestApps.OrchardCore.OpenAI.Core.Services;
using CrestApps.OrchardCore.OpenAI.Drivers;
using CrestApps.OrchardCore.OpenAI.Endpoints;
using CrestApps.OrchardCore.OpenAI.Indexes;
using CrestApps.OrchardCore.OpenAI.Migrations;
using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.Recipes;
using CrestApps.OrchardCore.OpenAI.Services;
using CrestApps.OrchardCore.OpenAI.ViewModels;
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
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.ResourceManagement;

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
            .Configure<OpenAIMarkdownPipelineOptions>(o =>
            {
                o.MarkdownPipelineBuilder.Configure("advanced");
            })
            .AddScoped<IOpenAIMarkdownService, OpenAIMarkdownService>()
            .AddScoped<IOpenAIFunctionService, DefaultOpenAIFunctionService>()
            .AddScoped<IDisplayDriver<OpenAIDeployment>, OpenAIDeploymentDisplayDriver>()
            .AddTransient<IConfigureOptions<OpenAIConnectionOptions>, OpenAIConnectionOptionsConfiguration>()
            .AddNavigationProvider<OpenAIAdminMenu>();
    }
}

[Feature(OpenAIConstants.Feature.ChatGPT)]
public sealed class ChatStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOpenAIChatTool<GetWeatherOpenAITool, GetWeatherOpenAIFunction>();

        services
            .AddOpenAIChatProfileServices()
            .AddTransient<IConfigureOptions<ResourceManagementOptions>, ResourceManagementOptionsConfiguration>()
            .AddNavigationProvider<OpenAIAdminChatMenu>()
            .AddDataMigration<OpenAIChatSessionIndexMigrations>()
            .AddIndexProvider<OpenAIChatSessionIndexProvider>()
            .AddDisplayDriver<OpenAIChatProfile, OpenAIChatProfileDisplayDriver>()
            .AddDisplayDriver<OpenAIChatSession, OpenAIChatSessionDisplayDriver>()
            .AddDisplayDriver<OpenAIChatListOptions, OpenAIChatListOptionsDisplayDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddOpenAIChatCompletionEndpoint<ChatStartup>()
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
