using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Ollama.Services;
using CrestApps.OrchardCore.Ollama;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Ollama;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIProfile<OllamaAIChatCompletionClient>(OllamaConstants.ImplementationName, OllamaConstants.ProviderName, o =>
        {
            o.DisplayName = "Ollama AI Chat";
            o.Description = "Provides AI profiles using Ollama AI Chat.";
        });
    }
}
