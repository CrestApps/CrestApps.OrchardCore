using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// What the AI usage report is told about a call: measured as it happens, and handed over once, when the
/// assistant's part of the call ends.
/// </summary>
/// <remarks>
/// None of this may cost the call anything, so nothing here throws: the tracker writes the summary in a scope of
/// its own and logs rather than fails.
/// </remarks>
public sealed partial class VoiceAgentConversationLoop
{
    /// <summary>
    /// Measures the assistant's speech on a turn-based call, on the time the provider says it happened.
    /// </summary>
    /// <param name="voiceEvent">The speech started or speech ended event.</param>
    /// <param name="started">Whether the speech started.</param>
    private void MeterTurnBasedSpeech(VoiceAgentEvent voiceEvent, bool started)
        => _sessionTracker.TurnBasedSpeech(voiceEvent.ActivityId, started, voiceEvent.OccurredUtc ?? _clock.UtcNow);

    /// <summary>
    /// Hands over the summary of a live session that has just ended.
    /// </summary>
    /// <param name="voiceEvent">The answered event the session was started from.</param>
    /// <param name="meter">What the session measured; never started when no session held the call.</param>
    /// <param name="deploymentName">The deployment the session was held on.</param>
    /// <param name="sessionHeldTheCall">Whether the session returned normally having held the call.</param>
    private Task RecordRealtimeSessionAsync(VoiceAgentEvent voiceEvent, AIVoiceSessionMeter meter, string deploymentName, bool sessionHeldTheCall)
    {
        var measured = meter.Measure(DateTime.UtcNow.Ticks);

        // No session ever held the call -- the provider carries no live media, or the session could not be
        // opened -- so the turn-based loop takes it, and that is what it is reported as.
        if (measured is null)
        {
            return Task.CompletedTask;
        }

        return _sessionTracker.RecordAsync(new AIVoiceSessionDraft
        {
            ActivityId = voiceEvent.ActivityId,
            ProviderName = voiceEvent.ProviderName,
            ProviderCallId = voiceEvent.ProviderCallId,
            Engine = AIVoiceSessionEngine.Realtime,
            DeploymentName = deploymentName,

            // A session that started and then threw did not end the way anybody decided.
            Outcome = AIVoiceSessionOutcomes.ForRealtime(
                _handoffTurn.HandoffRequested,
                _endCallTurn.EndCallRequested,
                _endCallTurn.ReachedVoicemail,
                failed: meter.HasFailed || !sessionHeldTheCall),
            WasAnswered = true,
            EndedUtc = measured.EndedUtc,
            Measurements = measured,
        });
    }

    /// <summary>
    /// Hands over the summary of a turn-based call whose assistant part has ended.
    /// </summary>
    /// <param name="voiceEvent">The event that ended it.</param>
    /// <param name="activity">The call's activity.</param>
    /// <param name="outcome">How it ended, when known here; otherwise it is read from what the call left.</param>
    /// <param name="endedUtc">When the assistant's part ended.</param>
    /// <param name="onlyWhenMeasured">Whether to write nothing when this node did not measure the call.</param>
    private Task RecordTurnBasedSessionAsync(
        VoiceAgentEvent voiceEvent,
        OmnichannelActivity activity,
        AIVoiceSessionOutcome? outcome,
        DateTime endedUtc,
        bool onlyWhenMeasured = false)
    {
        if (activity is null)
        {
            return Task.CompletedTask;
        }

        var measured = _sessionTracker.EndTurnBased(activity.ItemId, endedUtc);

        if (measured is null && onlyWhenMeasured)
        {
            return Task.CompletedTask;
        }

        // A call nobody measured is still summarized -- a call that rang out, or one whose events reached another
        // node -- and when a live session did measure it, its summary takes precedence over this one.
        return _sessionTracker.RecordAsync(new AIVoiceSessionDraft
        {
            ActivityId = activity.ItemId,
            ProviderName = voiceEvent.ProviderName,
            ProviderCallId = voiceEvent.ProviderCallId,
            Engine = AIVoiceSessionEngine.TurnBased,
            Outcome = outcome,
            WasAnswered = measured is not null || activity.AiEscalated || activity.Status == ActivityStatus.InProgress,
            EndedUtc = endedUtc,
            Measurements = measured,
        });
    }
}
