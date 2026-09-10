using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx;

/// <summary>
/// Registers the Telnyx AI Voice Agent: the Phone omnichannel processor that originates the call, the Telnyx
/// Call Control dialling client, and the adapter that translates Telnyx webhooks into the events the
/// provider-neutral automated voice conversation is driven by.
/// </summary>
[Feature(TelnyxConstants.Feature.AiVoice)]
public sealed class AiVoiceStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ITelnyxVoiceAgentClient, TelnyxVoiceAgentClient>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOmnichannelProcessor, VoiceOmnichannelProcessor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITelnyxAiVoiceEventHandler, TelnyxAiVoiceConversationHandler>());
    }
}
