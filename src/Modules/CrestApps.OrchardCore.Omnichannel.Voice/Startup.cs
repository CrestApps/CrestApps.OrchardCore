using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        // Speech-to-speech needs a bidirectional media path to the caller's leg, which only the Contact Center
        // Voice Media feature provides. This feature does not depend on it - automated calls run perfectly well
        // on speak and transcribe alone - so the capability is optional: without it, this reports that no
        // realtime session ran and the turn-based loop takes the call.
        services.TryAddScoped<IRealtimeVoiceConversationRunner, NoRealtimeVoiceConversationRunner>();
    }
}

/// <summary>
/// Adds the live speech-to-speech path, which is only possible where something can carry the call's audio both
/// ways.
/// </summary>
/// <remarks>
/// Replaces the reporting no-op the feature registers by default. <c>Replace</c> rather than <c>TryAdd</c>
/// because that default is always registered, and this exists precisely to take over from it whichever startup
/// runs first.
/// </remarks>
[RequireFeatures(ContactCenterConstants.Feature.VoiceMedia)]
public sealed class RealtimeVoiceStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IRealtimeVoiceConversationRunner, RealtimeVoiceConversationRunner>());
    }
}
