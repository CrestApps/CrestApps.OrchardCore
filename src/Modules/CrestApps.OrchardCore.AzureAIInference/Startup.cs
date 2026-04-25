using CrestApps.Core.AI;
using CrestApps.Core.AI.AzureAIInference;
using CrestApps.Core.AI.AzureAIInference.Services;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AzureAIInference.Drivers;
using CrestApps.OrchardCore.AzureAIInference.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AzureAIInference;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient<IAIProviderConnectionHandler, AzureAIInferenceConnectionHandler>());
        services
            .AddScoped<IAIClientProvider, AzureAIInferenceClientProvider>()
            .AddCoreAIProfile<ProviderAICompletionClient<AzureAIInferenceClientMarker>>(AzureAIInferenceConstants.ClientName, o =>
            {
                o.DisplayName = S["Azure AI Inference (GitHub Models)"];
                o.Description = S["Provides AI profiles using Azure AI Inference (GitHub Models)."];
            });

        services
            .AddCoreAIDeploymentProvider(AzureAIInferenceConstants.ClientName, o =>
            {
                o.DisplayName = S["Azure AI Inference"];
                o.Description = S["Azure AI Inference model deployments."];
            });
    }
}

[RequireFeatures(AIConstants.Feature.ConnectionManagement)]
public sealed class ConnectionManagementStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ConnectionManagementStartup(IStringLocalizer<ConnectionManagementStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICatalogEntryHandler<AIProviderConnection>, AzureAIInferenceConnectionSettingsHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Transient<IAIProviderConnectionHandler, AzureAIInferenceConnectionHandler>());
        services.AddDisplayDriver<AIProviderConnection, AzureAIInferenceConnectionDisplayDriver>();
        services.AddCoreAIConnectionSource(AzureAIInferenceConstants.ClientName, o =>
        {
            o.DisplayName = S["Azure AI Inference"];
            o.Description = S["Provides a way to configure Azure AI Inference connections."];
        });
    }
}
