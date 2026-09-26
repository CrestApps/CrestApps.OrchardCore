using CrestApps.OrchardCore.Omnichannel.Voice.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Follows automated calls for the AI usage report: measures a turn-based call across the provider events it is
/// made of, and writes one summary per call when the assistant's part of it ends.
/// </summary>
/// <remarks>
/// A turn-based call keeps no state between provider events, so what is measured across them is held here, in
/// memory, until the call ends. A call whose events reach more than one node is summarized without the audio
/// measurements the other node saw, rather than with half of them.
/// </remarks>
public interface IAIVoiceSessionTracker
{
    /// <summary>
    /// A turn-based call was answered.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="answeredUtc">When it was answered.</param>
    void BeginTurnBased(string activityId, DateTime answeredUtc);

    /// <summary>
    /// The provider reported the assistant's speech starting or finishing on a turn-based call.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="started"><see langword="true"/> when it started; <see langword="false"/> when it finished.</param>
    /// <param name="occurredUtc">When.</param>
    void TurnBasedSpeech(string activityId, bool started, DateTime occurredUtc);

    /// <summary>
    /// Ends the measurement of a turn-based call and returns it, or <see langword="null"/> when this node never
    /// saw the call answered.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="endedUtc">When the assistant's part of the call ended.</param>
    AIVoiceSessionMeasurements EndTurnBased(string activityId, DateTime endedUtc);

    /// <summary>
    /// Writes the call's summary in a scope of its own, so it survives the request the call ran in. Never throws.
    /// </summary>
    /// <param name="draft">What the loop knows about the call.</param>
    Task RecordAsync(AIVoiceSessionDraft draft);
}
