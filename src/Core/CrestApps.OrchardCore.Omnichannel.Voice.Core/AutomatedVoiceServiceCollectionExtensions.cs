using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.Core.Omnichannel.Voice;
using CrestApps.Core.Omnichannel.Voice.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Core;

/// <summary>
/// Registers automated voice conversations, independently of the host they run in.
/// </summary>
public static class AutomatedVoiceServiceCollectionExtensions
{
    /// <summary>
    /// Adds the conversation loop an automated call runs, and the default for the speech-to-speech path.
    /// </summary>
    /// <remarks>
    /// The end-call tool and its registration in the host's tool catalog stay with the host, because what a
    /// tool catalog is and how a tool is described to an operator is the host's business.
    /// </remarks>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreOmnichannelAutomatedVoice(this IServiceCollection services)
    {
        // Registered as itself as well as behind the interface: when a live session ends, the call is finished
        // in a child scope that resolves a fresh loop of its own, because the scope the session ran in belongs
        // to a webhook request the provider has long since abandoned.
        services.AddScoped<VoiceAgentConversationLoop>();
        services.AddScoped<IVoiceAgentConversationLoop>(serviceProvider => serviceProvider.GetRequiredService<VoiceAgentConversationLoop>());
        services.AddScoped<IRealtimeCallCompletionRunner, RealtimeCallCompletionRunner>();

        // One per call, so the tool and the session holding the line share an instance and two calls running at
        // once cannot end each other.
        services.TryAddScoped<IVoiceCallEndTurn, VoiceCallEndTurn>();

        // Speech-to-speech needs a bidirectional media path to the caller's leg, which not every host provides.
        // Automated calls run perfectly well on speak and transcribe alone, so the capability is optional:
        // without it, this reports that no realtime session ran and the turn-based loop takes the call.
        services.TryAddScoped<IRealtimeVoiceConversationRunner, NoRealtimeVoiceConversationRunner>();

        return services;
    }
}
