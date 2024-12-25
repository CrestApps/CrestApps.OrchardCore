using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Models;
using CrestApps.OrchardCore.OpenAI.Drivers;
using CrestApps.OrchardCore.OpenAI.Indexes;
using CrestApps.OrchardCore.OpenAI.Migrations;
using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.UrlRewriting.Recipes;

namespace CrestApps.OrchardCore.OpenAI;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddAIChatProfileServices()
            .AddNavigationProvider<OpenAIAdminMenu>();

        services
            .AddDataMigration<AIChatSessionIndexMigrations>()
            .AddIndexProvider<AIChatSessionIndexProvider>();

        services.AddScoped<IDisplayDriver<AIChatProfile>, AIChatProfileDisplayDriver>();
        services.AddScoped<IDisplayDriver<AIChatSession>, AIChatSessionDisplayDriver>();
        services.AddScoped<IDisplayDriver<AIChatListOptions>, AIChatListOptionsDisplayDriver>();
        services.Configure<StoreCollectionOptions>(o => o.Collections.Add(OpenAIConstants.CollectionName));
    }
}

[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIChatProfileStep>();
    }
}
