using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AzureAIInference.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AzureAIInference;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddAIProfile<AzureAIInferenceProfileSource, AzureAIInferenceCompletionClient>(AzureAIInferenceProfileSource.ImplementationName);

        services
            .AddAIDeploymentProvider<AzureAIInferenceDeploymentProvider>(AzureAIInferenceProfileSource.ProviderTechnicalName);
    }
}

