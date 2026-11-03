using CrestApps.Core.AI;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Providers.CustomLlm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Registers the custom LLM provider, which talks to your own WebSocket server.
/// </summary>
[Feature(AIConstants.Feature.CustomLlm)]
public sealed class CustomLlmStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAIClientProvider, CustomLlmClientProvider>());

        services.AddCoreAICompletionClient<ProviderAICompletionClient<CustomLlmClientMarker>>(CustomLlmClientMarker.Name, options =>
        {
            options.DisplayName = new LocalizedString("Custom LLM", "Custom LLM (WebSocket)");
            options.Description = new LocalizedString("Custom LLM", "Send each turn to your own server over a WebSocket.");
        });

        services.AddCoreAIConnectionSource(CustomLlmClientMarker.Name, options =>
        {
            options.DisplayName = new LocalizedString("Custom LLM", "Custom LLM (WebSocket)");
            options.Description = new LocalizedString("Custom LLM", "Your own server's wss:// address, and an API key it checks.");
        });

        services.AddCoreAIDeploymentProvider(CustomLlmClientMarker.Name, options =>
        {
            options.DisplayName = new LocalizedString("Custom LLM", "Custom LLM (WebSocket)");
            options.Description = new LocalizedString("Custom LLM", "A model or agent on your own WebSocket server.");
        });
    }
}
