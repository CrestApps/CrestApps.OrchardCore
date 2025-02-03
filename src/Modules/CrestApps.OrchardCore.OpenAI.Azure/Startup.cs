using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Azure.Drivers;
using CrestApps.OrchardCore.OpenAI.Azure.Migrations;
using CrestApps.OrchardCore.OpenAI.Azure.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.OpenAI.Azure;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
    }
}

[Feature(AzureOpenAIConstants.Feature.Deployments)]
public sealed class DeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<AzureOpenAIModelService>();
        services.AddScoped<AzureOpenAIDeploymentsService>();

        services
            .AddAIDeploymentProvider<AzureAIDeploymentProvider>(AzureOpenAIConstants.AzureProviderName)
            .AddDisplayDriver<AIDeployment, AzureOpenAIDeploymentDisplayDriver>()
            .AddScoped<AzureCognitiveServicesAccountServices>();
    }
}

[Feature(AzureOpenAIConstants.Feature.Deployments)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ImportAzureOpenAIDeploymentStep>();
    }
}

[Feature(AzureOpenAIConstants.Feature.Standard)]
public sealed class StandardStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIChatCompletionService<AzureOpenAIChatCompletionService>(AzureProfileSource.Key);
        services.AddAIChatProfileSource<AzureProfileSource>(AzureProfileSource.Key);
        services.AddDataMigration<AzureTitleGeneratorProfileMigrations>();
    }
}

[Feature(AzureOpenAIConstants.Feature.AISearch)]
public sealed class AISearchStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIChatCompletionService<AzureOpenAIWithSearchAIChatCompletionService>(AzureWithAzureAISearchProfileSource.Key);
        services.AddAIChatProfileSource<AzureWithAzureAISearchProfileSource>(AzureWithAzureAISearchProfileSource.Key);
        services.AddDisplayDriver<AIChatProfile, AzureOpenAIChatProfileSearchAIDisplayDriver>();
        services.AddScoped<IAIChatProfileHandler, AzureOpenAIChatProfileWithAISearchHandler>();
        services.AddDataMigration<AzureAISearchTitleGeneratorProfileMigrations>();
    }
}
