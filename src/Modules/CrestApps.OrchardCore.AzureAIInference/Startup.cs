using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AzureAIInference.Drivers;
using CrestApps.OrchardCore.AzureAIInference.Handlers;
using CrestApps.OrchardCore.AzureAIInference.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AzureAIInference;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddAIProfile<AzureAIInferenceCompletionClient>(AzureAIInferenceConstants.ImplementationName, AzureAIInferenceConstants.ProviderName, o =>
            {
                o.DisplayName = "Azure AI Inference (GitHub Models)";
                o.Description = "Provides AI profiles using Azure AI Inference (GitHub Models).";
            });

        services
            .AddAIDeploymentProvider(AzureAIInferenceConstants.ProviderName, o =>
            {
                o.DisplayName = "Azure AI Inference";
                o.Description = "Azure AI Inference AI deployments.";
            });
    }
}

[RequireFeatures(AIConstants.Feature.ConnectionManagement)]
public sealed class ConnectionManagementStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IModelHandler<AIProviderConnection>, AzureAIInferenceConnectionSettingsHandler>();
        services.AddTransient<IAIProviderConnectionHandler, AzureAIInferenceConnectionHandler>();
        services.AddDisplayDriver<AIProviderConnection, AzureAIInferenceConnectionDisplayDriver>();
        services.AddAIConnectionSource(AzureAIInferenceConstants.ProviderName, o =>
        {
            o.DisplayName = "Azure AI Inference";
            o.Description = "Provides a way to Configure Azure AI Inference connections.";
        });
    }
}
