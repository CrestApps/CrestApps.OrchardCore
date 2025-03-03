using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Ollama.Services;
using CrestApps.OrchardCore.Ollama;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Ollama;

internal sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIProfile<OllamaAIChatCompletionClient>(OllamaConstants.ImplementationName, OllamaConstants.ProviderName, o =>
        {
            o.DisplayName = S["Ollama AI Chat"];
            o.Description = S["Provides AI profiles using Ollama AI Chat."];
        });
    }
}
