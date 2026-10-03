using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Telnyx.Drivers;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx;

/// <summary>
/// Registers the Telnyx AI Voice Agent: the Phone omnichannel processor that originates the call, the Telnyx
/// Call Control dialling client, the adapter that translates Telnyx webhooks into the events the
/// provider-neutral automated voice conversation is driven by, and the answerer that hands an inbound call an entry
/// point routes to an AI voice agent to that conversation.
/// </summary>
[Feature(TelnyxConstants.Feature.AiVoice)]
public sealed class AiVoiceStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ITelnyxVoiceAgentClient, TelnyxVoiceAgentClient>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOmnichannelProcessor, VoiceOmnichannelProcessor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITelnyxAiVoiceEventHandler, TelnyxAiVoiceConversationHandler>());

        // An inbound call an entry point routes to an AI voice agent is answered with the AI voice leg's state, so its
        // events reach the same conversation a call the AI places does. The picker for the agent is on the entry point.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IInboundAIVoiceAnswerer, TelnyxInboundAIVoiceAnswerer>());
        services.AddDisplayDriver<ContactCenterEntryPoint, TelnyxEntryPointAIAgentDisplayDriver>();
    }
}
