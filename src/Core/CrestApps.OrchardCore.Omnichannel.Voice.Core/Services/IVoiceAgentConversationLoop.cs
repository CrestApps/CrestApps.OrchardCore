using CrestApps.OrchardCore.Omnichannel.Voice.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Drives an automated voice conversation from the events a telephony provider reports.
/// </summary>
public interface IVoiceAgentConversationLoop
{
    /// <summary>
    /// Advances the conversation for one call event.
    /// </summary>
    /// <param name="voiceEvent">What happened on the call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task HandleAsync(VoiceAgentEvent voiceEvent, CancellationToken cancellationToken = default);
}
