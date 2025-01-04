using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Azure.Drivers;
using CrestApps.OrchardCore.OpenAI.Azure.Recipes;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.OpenAI.Azure;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddHttpClient(AzureOpenAIConstants.HttpClientName)
            .AddStandardResilienceHandler(options =>
            {
                options.Retry = new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                };

                options.AttemptTimeout = new HttpTimeoutStrategyOptions()
                {
                    Timeout = TimeSpan.FromSeconds(30),
                };

                options.CircuitBreaker = new HttpCircuitBreakerStrategyOptions
                {
                    // The sampling duration of circuit breaker strategy needs to be at
                    // least double of an attempt timeout strategy’s timeout interval, in order to be effective.
                    SamplingDuration = TimeSpan.FromSeconds(60),
                };
            });
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
            .AddOpenAIDeploymentSource<AzureModelDeploymentSource>(AzureOpenAIConstants.AzureDeploymentSourceName)
            .AddScoped<IDisplayDriver<OpenAIDeployment>, AzureOpenAIDeploymentDisplayDriver>()
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
        services.AddOpenAIChatCompletionService<AzureChatCompletionService>(AzureProfileSource.Key);
        services.AddOpenAIChatProfileSource<AzureProfileSource>(AzureProfileSource.Key);
    }
}

[Feature(AzureOpenAIConstants.Feature.AISearch)]
public sealed class AISearchStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOpenAIChatCompletionService<AzureChatCompletionService>(AzureWithAzureAISearchProfileSource.Key);
        services.AddOpenAIChatProfileSource<AzureWithAzureAISearchProfileSource>(AzureWithAzureAISearchProfileSource.Key);
        services.AddScoped<IDisplayDriver<OpenAIChatProfile>, AzureOpenAIChatProfileSearchAIDisplayDriver>();
        services.AddScoped<IOpenAIChatProfileHandler, AzureOpenAIChatProfileWithAISearchHandler>();
    }
}
