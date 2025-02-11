using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.OpenAI.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAICompletionService<OpenAIChatCompletionService>(OpenAIDeploymentProvider.ProviderName);
        services.AddAIProfileSource<OpenAIProfileSource>(OpenAIDeploymentProvider.ProviderName);
        services.AddAIDeploymentProvider<OpenAIDeploymentProvider>(OpenAIDeploymentProvider.ProviderName);
    }
}

