using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Azure.Drivers;
using CrestApps.OrchardCore.OpenAI.Azure.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.OpenAI.Azure;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<AzureOpenAIModelService>();
        services.AddScoped<AzureOpenAIDeploymentsService>();

        services
            .AddAIDeploymentProvider<AzureAIDeploymentProvider>(AzureOpenAIConstants.AzureProviderName)
            .AddScoped<AzureCognitiveServicesAccountServices>();
    }
}

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
        services.AddAICompletionService<AzureOpenAICompletionService>(AzureProfileSource.Key);
        services.AddAIProfileSource<AzureProfileSource>(AzureProfileSource.Key);
    }
}

[Feature(AzureOpenAIConstants.Feature.AISearch)]
public sealed class AISearchStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAICompletionService<AzureAISearchCompletionService>(AzureWithAzureAISearchProfileSource.Key);
        services.AddAIProfileSource<AzureWithAzureAISearchProfileSource>(AzureWithAzureAISearchProfileSource.Key);
        services.AddDisplayDriver<AIProfile, AzureOpenAIProfileSearchAIDisplayDriver>();
        services.AddScoped<IAIProfileHandler, AzureOpenAIProfileWithAISearchHandler>();
    }
}
