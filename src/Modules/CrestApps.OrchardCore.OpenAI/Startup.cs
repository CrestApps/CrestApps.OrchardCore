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
        services
            .AddModelDeploymentServices()
            .AddScoped<IDisplayDriver<ModelDeployment>, ModelDeploymentDisplayDriver>()
            .AddTransient<IConfigureOptions<OpenAIConnectionOptions>, OpenAIConnectionOptionsConfiguration>()
            .AddNavigationProvider<OpenAIAdminMenu>();
    }
}

[Feature(OpenAIConstants.Feature.ChatGPT)]
public sealed class ChatStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIChatFunction<GetWeatherFunction>("get_current_weather");

        services
            .AddAIChatProfileServices()
            .AddTransient<IConfigureOptions<ResourceManagementOptions>, ResourceManagementOptionsConfiguration>()
            .AddNavigationProvider<OpenAIAdminChatMenu>()
            .AddDataMigration<AIChatSessionIndexMigrations>()
            .AddIndexProvider<AIChatSessionIndexProvider>()
            .AddScoped<IDisplayDriver<AIChatProfile>, AIChatProfileDisplayDriver>()
            .AddScoped<IDisplayDriver<AIChatSession>, AIChatSessionDisplayDriver>()
            .AddScoped<IDisplayDriver<AIChatListOptions>, AIChatListOptionsDisplayDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddOpenAIChatEndpoint<ChatStartup>();
    }
}

[Feature(OpenAIConstants.Feature.Area)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ModelDeploymentStep>();
    }
}

[Feature(OpenAIConstants.Feature.ChatGPT)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ChatRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIChatProfileStep>();
    }
}
