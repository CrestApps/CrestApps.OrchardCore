using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Ollama.Drivers;
using CrestApps.OrchardCore.AI.Ollama.Services;
using CrestApps.OrchardCore.DeepSeek.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Ollama;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIChatProfile, OllamaChatProfileDisplayDriver>();
        services.AddAIChatCompletionService<OllamaAIChatCompletionService>(OllamaProfileSource.Key);
        services.AddAIChatProfileSource<OllamaProfileSource>(OllamaProfileSource.Key);
        services.AddTransient<IConfigureOptions<DefaultOllamaOptions>, DefaultOllamaOptionsConfiguration>();
    }
}
