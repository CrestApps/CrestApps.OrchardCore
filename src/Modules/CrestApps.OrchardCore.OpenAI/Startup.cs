using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Drivers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOpenAIChatServices();
    }
}

[Feature(OpenAIConstants.Feature.ChatGPT)]
public sealed class ChatGPTStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDisplayDriver<AIChatProfile, OpenAIChatProfileDisplayDriver>();
    }
}
