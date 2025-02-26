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
            .AddAIDeploymentProvider(AzureOpenAIConstants.ProviderName, o =>
            {
                o.DisplayName = "Azure OpenAI";
                o.Description = "Azure OpenAI AI deployments.";
            })
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
        services.AddAIProfile<AzureOpenAICompletionClient>(AzureOpenAIConstants.StandardImplementationName, AzureOpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = "Azure OpenAI";
            o.Description = "Provides AI profiles using Azure OpenAI models.";
        });
    }
}

[Feature(AzureOpenAIConstants.Feature.AISearch)]
public sealed class AISearchStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIProfile<AzureAISearchCompletionClient>(AzureOpenAIConstants.AISearchImplementationName, AzureOpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = "Azure OpenAI with Azure AI Search";
            o.Description = "Provides AI profiles using Azure OpenAI models with your data.";
        });
        services.AddDisplayDriver<AIProfile, AzureOpenAIProfileSearchAIDisplayDriver>();
        services.AddScoped<IAIProfileHandler, AzureAISearchAIProfileHandler>();
    }
}
