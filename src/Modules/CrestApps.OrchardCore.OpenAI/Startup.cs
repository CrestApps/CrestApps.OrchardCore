using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Drivers;
using CrestApps.OrchardCore.OpenAI.Endpoints;
using CrestApps.OrchardCore.OpenAI.Indexes;
using CrestApps.OrchardCore.OpenAI.Migrations;
using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.Recipes;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.OpenAI;

public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(o => o.Collections.Add(OpenAIConstants.CollectionName));

        // Register model deployments service.
        services
            .AddModelDeploymentServices()
            .AddScoped<IDisplayDriver<ModelDeployment>, ModelDeploymentDisplayDriver>()
            .Configure<OpenAIConnectionOptions>(options =>
            {
                var jsonNode = _shellConfiguration.GetSection("CrestApps_OpenAI_Connections").AsJsonNode();

                var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonNode);

                var obj = JsonObject.Create(jsonElement, new JsonNodeOptions()
                {
                    PropertyNameCaseInsensitive = true,
                });

                if (obj == null)
                {
                    return;
                }

                foreach (var element in obj)
                {
                    var entries = new List<OpenAIConnectionEntry>();

                    if (element.Value is JsonArray items)
                    {
                        // If the value is an array, deserialize it to a list of JsonObjects.
                        options.Connections[element.Key] = JsonSerializer.Deserialize<List<OpenAIConnectionEntry>>(items);
                    }
                    else if (element.Value is JsonObject jObject)
                    {
                        // If the value is a single object, create a list with that single object.
                        options.Connections[element.Key] =
                        [
                            JsonSerializer.Deserialize<OpenAIConnectionEntry>(jObject),
                        ];
                    }
                }
            });

        // Register AI Chat services.
        services
            .AddAIChatProfileServices()
            .AddNavigationProvider<OpenAIAdminMenu>()
            .AddDataMigration<AIChatSessionIndexMigrations>()
            .AddIndexProvider<AIChatSessionIndexProvider>()
            .AddScoped<IDisplayDriver<AIChatProfile>, AIChatProfileDisplayDriver>()
            .AddScoped<IDisplayDriver<AIChatSession>, AIChatSessionDisplayDriver>()
            .AddScoped<IDisplayDriver<AIChatListOptions>, AIChatListOptionsDisplayDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddOpenAIChatEndpoint<Startup>();
    }
}

[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIChatProfileStep>();
        services.AddRecipeExecutionStep<ModelDeploymentStep>();
    }
}
