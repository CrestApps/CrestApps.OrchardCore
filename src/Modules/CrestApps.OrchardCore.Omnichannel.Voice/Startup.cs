using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Voice;

/// <summary>
/// Registers the provider-neutral automated voice conversation loop. The audio itself comes from whichever
/// telephony provider registers an <c>IVoiceAgentMediaProvider</c>.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IVoiceAgentConversationLoop, VoiceAgentConversationLoop>();
    }
}
