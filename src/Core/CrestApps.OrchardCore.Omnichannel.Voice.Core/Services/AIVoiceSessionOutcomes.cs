using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// How an automated conversation ended, as the usage report names it.
/// </summary>
internal static class AIVoiceSessionOutcomes
{
    /// <summary>
    /// The outcome of a live session, from what the session itself decided.
    /// </summary>
    /// <param name="handoffRequested">Whether the model asked to transfer the caller to a person.</param>
    /// <param name="endCallRequested">Whether the model ended the call.</param>
    /// <param name="reachedVoicemail">Whether the model said it had reached a voicemail.</param>
    /// <param name="failed">Whether the session reported an error.</param>
    public static AIVoiceSessionOutcome ForRealtime(bool handoffRequested, bool endCallRequested, bool reachedVoicemail, bool failed)
    {
        // A transfer the model asked for is carried out whatever else happened on the way out of the session.
        if (handoffRequested)
        {
            return AIVoiceSessionOutcome.HandedToAgent;
        }

        if (endCallRequested)
        {
            return reachedVoicemail ? AIVoiceSessionOutcome.Voicemail : AIVoiceSessionOutcome.CompletedByAI;
        }

        return failed ? AIVoiceSessionOutcome.Failed : AIVoiceSessionOutcome.CallerHungUp;
    }

    /// <summary>
    /// The outcome of a turn-based call, from the activity and transcript it left.
    /// </summary>
    /// <param name="activity">The call's activity, read after the call ended.</param>
    /// <param name="prompts">The call's transcript.</param>
    /// <param name="wasAnswered">Whether the call was answered.</param>
    public static AIVoiceSessionOutcome ForTurnBased(OmnichannelActivity activity, IReadOnlyList<AIChatSessionPrompt> prompts, bool wasAnswered)
    {
        ArgumentNullException.ThrowIfNull(activity);

        prompts ??= [];

        if (activity.AiEscalated)
        {
            return AIVoiceSessionOutcome.HandedToAgent;
        }

        // Before the failed status: the no-response expiry pass fails a call nobody picked up, and that call is
        // unanswered, not broken.
        if (!wasAnswered)
        {
            return AIVoiceSessionOutcome.NoAnswer;
        }

        if (VoicemailGreeting.ReachedVoicemail(activity.TryGet<VoicemailReached>(out var voicemail) ? voicemail : null, prompts))
        {
            return AIVoiceSessionOutcome.Voicemail;
        }

        if (activity.Status == ActivityStatus.Failed)
        {
            return AIVoiceSessionOutcome.Failed;
        }

        // The marker on the last line is the assistant saying goodbye and hanging up.
        var lastAssistant = prompts.LastOrDefault(prompt => prompt.Role == ChatRole.Assistant);

        return lastAssistant?.Content?.Contains(VoiceAgentConversationLoop.HangupMarker, StringComparison.Ordinal) == true
            ? AIVoiceSessionOutcome.CompletedByAI
            : AIVoiceSessionOutcome.CallerHungUp;
    }
}
