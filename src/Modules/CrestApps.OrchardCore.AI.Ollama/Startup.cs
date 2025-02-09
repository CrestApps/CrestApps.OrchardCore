using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Ollama.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Ollama;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIChatCompletionService<OllamaAIChatCompletionService>(OllamaProfileSource.Key);
        services.AddAIChatProfileSource<OllamaProfileSource>(OllamaProfileSource.Key);
    }
}
