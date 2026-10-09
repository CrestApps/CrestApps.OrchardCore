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

    /// <summary>
    /// Concludes a call whose end the provider never reported, the way its hangup would have: a call handed to a
    /// person is left to them, and anything else is reviewed and dispositioned. See
    /// <see cref="StrandedVoiceCallPolicy"/> for when a call counts as over.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the call is being concluded; <see langword="false"/> when it was left alone.</returns>
    Task<bool> ConcludeStrandedCallAsync(string activityId, CancellationToken cancellationToken = default);
}
