using CrestApps.Core;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Telemetry;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// One turn of the turn-based loop: the caller's final transcript is stored, the model is asked for a reply, and the
/// reply is spoken, each turn measured as a <c>crestapps.voice.ai.turn</c> span.
/// </summary>
public sealed partial class VoiceAgentConversationLoop
{
    // How a turn-based turn ended without a reply, for the outcome of crestapps.voice.ai.turn.duration.
    private const string TurnIgnored = "ignored";
    private const string TurnDuplicate = "duplicate";
    private const string TurnVoicemail = "voicemail";
    private const string TurnOvertaken = "overtaken";
    private const string TurnNoReply = "no_reply";

    // One turn of the turn-based loop, from the caller's final transcript to the reply starting to play: a
    // crestapps.voice.ai.turn span, under which the completion's own gen_ai spans nest, and its duration.
    private async Task OnTranscriptionAsync(VoiceAgentEvent voiceEvent, IVoiceAgentMediaProvider media, CancellationToken cancellationToken)
    {
        if (!voiceEvent.TranscriptionIsFinal || string.IsNullOrWhiteSpace(voiceEvent.TranscriptionText))
        {
            return;
        }

        using var telemetry = TelemetryScope.Start(VoiceTelemetry.ActivitySource, "crestapps.voice.ai.turn", VoiceTelemetry.TurnDuration);

        telemetry.SetSpanTag(VoiceTelemetry.ActivityIdSpanTag, voiceEvent.ActivityId);
        telemetry.SetSpanTag(CrestAppsOrchardCoreTelemetry.ProviderTag, voiceEvent.ProviderName);

        try
        {
            telemetry.SetOutcome(await OnFinalTranscriptionAsync(voiceEvent, media, cancellationToken));
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);

            throw;
        }
    }

    // Returns how the turn ended, for the turn's outcome tag: ok when a reply was spoken.
    private async Task<string> OnFinalTranscriptionAsync(VoiceAgentEvent voiceEvent, IVoiceAgentMediaProvider media, CancellationToken cancellationToken)
    {
        var activity = await _activityStore.FindByIdAsync(voiceEvent.ActivityId, cancellationToken);

        if (activity is null || await IgnoreWhatIsHeardAfterHandoffAsync(voiceEvent, media, activity, cancellationToken))
        {
            return TurnIgnored;
        }

        var (profile, session) = await ResolveConversationAsync(activity, cancellationToken);

        if (profile is null || session is null)
        {
            return TurnIgnored;
        }

        // Stop listening while we think and speak, so the agent's own text-to-speech is never transcribed. Start
        // the stop now but let the LLM turn run concurrently, so its provider round-trip overlaps the model latency
        // rather than adding to it. The stop is awaited before we speak, so the ordering guarantee is preserved.
        var stopListening = media.StopTranscriptionAsync(voiceEvent.ProviderCallId, cancellationToken);

        var caller = voiceEvent.TranscriptionText.Trim();

        var prompts = await _promptStore.GetPromptsAsync(session.SessionId);
        var lastUser = prompts.LastOrDefault(p => p.Role == ChatRole.User);

        // Redelivery / duplicate final guard.
        if (lastUser is not null && string.Equals(lastUser.Content?.Trim(), caller, StringComparison.OrdinalIgnoreCase))
        {
            await stopListening;

            return TurnDuplicate;
        }

        // Before anything slow: the line is stored with this request, and a silence watch that fired while the reply
        // was still being written would find nothing from the caller.
        var heard = _silenceWatchdog.CallerHeard(voiceEvent.ActivityId);

        // Read before this turn is stored, so the turn being judged is not mistaken for somebody having already spoken.
        var turnsBefore = prompts.Count;
        var customerHadSpoken = prompts.Any(prompt => prompt.Role == ChatRole.User && !prompt.IsGeneratedPrompt && !string.IsNullOrWhiteSpace(prompt.Content));

        await StorePromptAsync(session, ChatRole.User, caller, EstimateCallerLineStart(voiceEvent, caller, prompts), cancellationToken);

        // A recording answering is not somebody to converse with, and must never be read as the customer's reply.
        if (await HandleVoicemailTurnAsync(voiceEvent, media, activity, profile, session, turnsBefore, customerHadSpoken, caller, stopListening, cancellationToken))
        {
            return TurnVoicemail;
        }

        var (reply, handoffRequested, handoffReason, endCallRequested) = await CompleteAsync(profile, session, activity, turnGuidance: null, cancellationToken);

        // A tool on this turn handed the call to another AI profile. The new profile answers now, in place of the old
        // one's goodbye: live, the old profile said it was connecting the caller, and then nobody spoke until the
        // caller did.
        if (!handoffRequested && !endCallRequested &&
            await FindSwitchedConversationAsync(activity, profile, cancellationToken) is { } switched)
        {
            activity = switched.Activity;
            session = switched.Session;
            (reply, handoffRequested, handoffReason, endCallRequested) = await CompleteAsync(switched.Profile, switched.Session, activity, ProfileSwitchedTurn, cancellationToken);
        }

        // The model recognised a voicemail the greeting check did not, and has left its message on it.
        if (endCallRequested && _endCallTurn.ReachedVoicemail)
        {
            activity.Put(new VoicemailReached { MessageLeft = true });
            await _activityStore.UpdateAsync(activity, cancellationToken);
        }

        await stopListening;

        if (!handoffRequested && ReplyWasOvertaken(voiceEvent, heard))
        {
            return TurnOvertaken;
        }

        if (string.IsNullOrWhiteSpace(reply) && !handoffRequested)
        {
            // Nothing to say; keep listening so the call is not stranded silent.
            var listenLanguage = await GetListenLanguageAsync(media, activity, cancellationToken);
            await media.StartTranscriptionAsync(voiceEvent.ProviderCallId, language: listenLanguage, commandId: $"ai-tx-retry-{prompts.Count}", cancellationToken);

            // The caller's turn has been stored since the prompts above were read, so it counts as said.
            await WatchForSilenceAsync(voiceEvent, prompts.Count + 1);

            return TurnNoReply;
        }

        if (!string.IsNullOrWhiteSpace(reply))
        {
            // The marker is how the speak.ended handler knows this line was the last one, and it is stripped
            // before anything is spoken or shown. Recorded on the turn rather than acted on here, because the
            // closing line has not been said yet: hanging up now would cut it off mid-word.
            await StorePromptAsync(
                session,
                ChatRole.Assistant,
                endCallRequested ? reply + " " + HangupMarker : reply,
                cancellationToken);
        }

        if (handoffRequested)
        {
            // The model invoked the transfer tool. Record a durable request on the activity so the speak.ended
            // handler bridges the call once the closing line finishes (a text marker is no longer used).
            activity.Put(new PendingVoiceHandoff { Reason = handoffReason });
            await _activityStore.UpdateAsync(activity, cancellationToken);
        }

        // Only the hangup marker remains a text control token; strip it so the caller never hears it.
        var spoken = (reply ?? string.Empty).Replace(HangupMarker, string.Empty, StringComparison.Ordinal).Trim();

        if (string.IsNullOrWhiteSpace(spoken) && handoffRequested)
        {
            // The model called the tool without a closing line; speak a neutral bridge line so the caller is not
            // met with silence before the transfer.
            spoken = "Thanks. Let me connect you with a specialist who can help. Please hold for just a moment.";
        }

        await SpeakAsync(media, voiceEvent.ProviderCallId, activity, spoken, cancellationToken);

        return CrestAppsOrchardCoreTelemetry.OutcomeOk;
    }
}
