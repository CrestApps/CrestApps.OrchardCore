using CrestApps.OrchardCore.Omnichannel.Voice.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Gathers the keys a caller presses on a turn-based call into one turn.
/// </summary>
/// <remarks>
/// A turn-based call has no session that outlives a webhook, and each key arrives on a webhook of its own. Taken as
/// a turn each, five keys were five turns answered at once: live, the caller heard five replies in a row about one
/// zip code and could not get a word in.
/// </remarks>
public interface ITurnBasedCallerKeys
{
    /// <summary>
    /// Adds the keys of one provider event. Once the caller stops pressing, every key collected is answered as one
    /// turn, in the order the keys were pressed.
    /// </summary>
    /// <param name="voiceEvent">The <see cref="VoiceAgentEventKind.DigitsReceived"/> event.</param>
    void Collect(VoiceAgentEvent voiceEvent);

    /// <summary>
    /// Drops keys collected on a call that has ended.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    void Forget(string activityId);
}
