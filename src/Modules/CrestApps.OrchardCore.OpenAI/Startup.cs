using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Functions;
using CrestApps.OrchardCore.OpenAI.Drivers;
using CrestApps.OrchardCore.OpenAI.Endpoints;
using CrestApps.OrchardCore.OpenAI.Indexes;
using CrestApps.OrchardCore.OpenAI.Migrations;
using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.Recipes;
using CrestApps.OrchardCore.OpenAI.Services;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using Fluid;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        });

        services
            .AddOpenAIDeploymentServices()
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
        services.AddOpenAIChatFunction<GetWeatherFunction>("get_current_weather");

        services
            .AddOpenAIChatProfileServices()
            .AddTransient<IConfigureOptions<ResourceManagementOptions>, ResourceManagementOptionsConfiguration>()
            .AddNavigationProvider<OpenAIAdminChatMenu>()
            .AddDataMigration<OpenAIChatSessionIndexMigrations>()
            .AddIndexProvider<OpenAIChatSessionIndexProvider>()
            .AddDisplayDriver<OpenAIChatProfile, OpenAIChatProfileDisplayDriver>()
            .AddDisplayDriver<OpenAIChatSession, AIChatSessionDisplayDriver>()
            .AddDisplayDriver<OpenAIChatListOptions, OpenAIChatListOptionsDisplayDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddOpenAIChatCompletionEndpoint<ChatStartup>();
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
