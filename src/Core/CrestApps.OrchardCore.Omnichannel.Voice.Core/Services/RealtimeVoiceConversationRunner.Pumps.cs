using CrestApps.Core.ContactCenter;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Omnichannel.Services;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Realtime;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.Core.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The two audio pumps a live session runs: the caller's microphone into the model, and the model's speech back
/// out to the call.
/// </summary>
/// <remarks>
/// Split from the session itself because they are the only part of it that runs continuously for the whole call,
/// and because the file had grown past the size the architecture guard allows.
/// </remarks>
public sealed partial class RealtimeVoiceConversationRunner
{
    /// <summary>
    /// Tells the session it is on a telephone, not a headset.
    /// </summary>
    /// <remarks>
    /// The provider's default turn detection assumes clean, close-mic audio. A call is 8 kHz, companded, and
    /// carries line noise and whatever leaks back from the far end's earpiece — so on the defaults the model
    /// decides the caller has started and stopped talking when they have done neither. Live calls showed it
    /// answering phantom turns ("Thank you." transcribed before anyone had spoken), transcribing the same
    /// utterance twice, and restarting its own question mid-sentence — which reads as the assistant talking to
    /// itself and never letting the caller speak.
    /// <para>
    /// Waiting longer for a pause and requiring more confidence that a pause is speech both push against that.
    /// Interruption stays on: being talked over is the other half of sounding like a machine.
    /// </para>
    /// </remarks>
    private async Task ApplyTelephonyTurnDetectionAsync(IRealtimeConversation conversation, CancellationToken cancellationToken)
    {
        try
        {
            // The detector type is left as the session already has it: the valid values belong to the provider,
            // and naming one here would be a guess that fails closed on a live call.
            await conversation.UpdateTurnDetectionAsync(
                allowInterruption: true,
                silenceDurationMs: TelephonySilenceDurationMilliseconds,
                vadThreshold: TelephonyVadThreshold,
                turnDetectionType: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Tuning is an improvement, not a precondition. A provider that will not accept it still carries
            // the call on its defaults.
            _logger.LogWarning(ex, "Could not apply telephony turn detection to a realtime voice session; the provider defaults stay in effect.");
        }
    }

    private async Task PumpCallerAudioAsync(
        IContactCenterVoiceMediaSession media,
        IRealtimeConversation conversation,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in media.ReadIncomingAsync(cancellationToken))
            {
                var audio = RealtimeAudioConverter.ToRealtime(frame.Data, media.IncomingFormat);

                if (!audio.IsEmpty)
                {
                    await conversation.SendAudioAsync(audio, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The call ended or the other pump stopped. Both are ordinary ends to a conversation.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The caller audio stream ended unexpectedly during a realtime voice session.");
        }
    }

    private async Task PumpAssistantAudioAsync(
        IContactCenterVoiceMediaSession media,
        IRealtimeConversation conversation,
        RealtimeVoiceConversationContext context,
        CallAmbience ambience,
        CancellationToken cancellationToken)
    {
        var assistantText = new System.Text.StringBuilder();

        // Set once the assistant is finished talking for good, and cleared the moment the customer speaks again.
        var goodbyeSaid = false;

        // The model finishes speaking its closing line and calls the end-call tool after it, so by the time the
        // request arrives the goodbye has usually already been said. Watching the request rather than reading it
        // when a line ends is what tells the two apart: the line in flight when it arrives is the goodbye and is
        // allowed to finish; anything the model starts afterwards is the repeat.
        var closingRequested = false;
        var utteranceInFlight = false;

        using var closingRegistration = context.EndCallRequested.CanBeCanceled
            ? context.EndCallRequested.Register(() =>
            {
                closingRequested = true;

                // Nothing was being said when the call was closed, so the goodbye is already behind us.
                if (!Volatile.Read(ref utteranceInFlight))
                {
                    goodbyeSaid = true;
                }
            })
            : default;

        // Every conversation-level fault found on this platform so far -- a transfer nobody asked for, a reply the
        // model never heard, a goodbye said twice -- was diagnosed from the stored transcript after the call,
        // because the turns themselves left no trace. Recorded at debug so a call can be followed as it happened:
        // when the caller started talking, what came back as their words, and when nothing did.
        var activityId = context.Activity?.ItemId.SanitizeLogValue();

        try
        {
            await foreach (var conversationEvent in conversation.GetEventsAsync(cancellationToken))
            {
                if (conversationEvent.Type is not RealtimeConversationEventType.AssistantAudioDelta
                                            and not RealtimeConversationEventType.AssistantTranscriptDelta &&
                    _logger.IsEnabled(LogLevel.Debug))
                {
                    // Sanitized into a local first: as an argument it would be evaluated on the way into a call
                    // that may discard it, and this runs for every event of a live call.
                    var text = conversationEvent.Text.SanitizeLogValue();

                    _logger.LogDebug(
                        "Realtime turn on activity '{ActivityId}': {EventType} '{Text}'.",
                        activityId,
                        conversationEvent.Type,
                        text);
                }

                switch (conversationEvent.Type)
                {
                    case RealtimeConversationEventType.AssistantAudioDelta:
                        var speech = conversationEvent.Audio;

                        // The model says its goodbye, calls the end-call tool, reads the tool's reply and — with
                        // the line still open while the customer is given their moment — says the very same
                        // goodbye again. Heard live, twice. Nothing here can stop it being generated; what it can
                        // do is not play it, and not stamp the clock the closing watchdog is counting down.
                        if (goodbyeSaid)
                        {
                            break;
                        }

                        utteranceInFlight = true;

                        if (!speech.IsEmpty)
                        {
                            // Stamped for two readers: the bed pump, which must stay quiet while the assistant is
                            // talking or the room doubles, and the closing watchdog, which waits for the goodbye
                            // to actually finish before it hangs up.
                            Interlocked.Exchange(ref _lastAssistantAudioTicks, DateTime.UtcNow.Ticks);
                        }

                        if (ambience is not null && !speech.IsEmpty)
                        {
                            // Mixed before conversion so the bed is resampled and companded with the voice, and
                            // arrives shaped by the same line as the speech rather than sitting on top of it.
                            var mixed = speech.ToArray();
                            ambience.MixIntoPcmBytes(mixed);
                            speech = mixed;
                        }

                        var audio = RealtimeAudioConverter.FromRealtime(speech, media.OutgoingFormat);

                        if (!audio.IsEmpty)
                        {
                            await media.WriteOutgoingAsync(new ContactCenterVoiceMediaFrame { Data = audio }, cancellationToken);
                        }

                        break;

                    case RealtimeConversationEventType.UserSpeechStarted:
                        // They are talking, so the call is not over after all and the assistant may answer.
                        goodbyeSaid = false;
                        closingRequested = false;

                        // The first syllable, not the finished sentence. A transcript only exists once the caller
                        // has stopped talking and the provider has transcribed them, which is seconds later --
                        // long enough that a call closing down would already have been cut. This is the moment
                        // the caller decides the conversation is not over, so it is the moment that has to count.
                        Interlocked.Exchange(ref _lastCallerSpeechTicks, DateTime.UtcNow.Ticks);

                        break;

                    case RealtimeConversationEventType.UserTranscript:
                        // The caller spoke and the provider returned nothing for it. Heard live as an assistant
                        // that ignores a short "yes" and waits for it to be said again, which reads to the person
                        // on the phone as not being listened to.
                        if (string.IsNullOrWhiteSpace(conversationEvent.Text) && _logger.IsEnabled(LogLevel.Information))
                        {
                            _logger.LogInformation(
                                "A caller utterance on activity '{ActivityId}' came back with no transcript, so the model never saw it.",
                                activityId);
                        }

                        // The caller said something. Stamped before the store so a closing call counts it even if
                        // persisting the turn takes a moment.
                        if (!string.IsNullOrWhiteSpace(conversationEvent.Text))
                        {
                            Interlocked.Exchange(ref _lastCallerSpeechTicks, DateTime.UtcNow.Ticks);
                        }

                        // Recorded so the call is concluded, summarized and dispositioned exactly the way a
                        // turn-based one is: everything downstream reads the transcript, not the audio.
                        await StorePromptAsync(context, ChatRole.User, conversationEvent.Text, cancellationToken);

                        break;

                    case RealtimeConversationEventType.UserTranscriptFailed:
                        // The provider took an utterance and could not transcribe it. Said at information level
                        // rather than debug because it is not a detail: a turn the caller took has been lost, the
                        // model is still waiting for them, and the caller believes they have already answered.
                        if (_logger.IsEnabled(LogLevel.Information))
                        {
                            _logger.LogInformation(
                                "A caller utterance on activity '{ActivityId}' could not be transcribed, so the model never saw it.",
                                activityId);
                        }

                        break;

                    case RealtimeConversationEventType.AssistantTranscriptDelta:
                        utteranceInFlight = true;
                        assistantText.Append(conversationEvent.Text);

                        break;

                    case RealtimeConversationEventType.AssistantTranscriptDone:
                        var spoken = assistantText.Length > 0 ? assistantText.ToString() : conversationEvent.Text;
                        assistantText.Clear();

                        // A line the customer never heard is not part of the conversation, so it is not written
                        // to the transcript either -- the record should say what was said on the call.
                        if (goodbyeSaid)
                        {
                            break;
                        }

                        await StorePromptAsync(context, ChatRole.Assistant, spoken, cancellationToken);

                        utteranceInFlight = false;

                        // The line that was in flight when the call was closed is the goodbye. It has now been
                        // said, so the assistant is done talking.
                        goodbyeSaid = closingRequested;

                        break;

                    case RealtimeConversationEventType.Error:
                        _logger.LogError(
                            "A realtime voice session reported an error on activity '{ActivityId}': {Error}",
                            context.Activity?.ItemId.SanitizeLogValue(),
                            conversationEvent.ErrorMessage.SanitizeLogValue());

                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The call ended. Nothing to report.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The realtime voice session ended unexpectedly.");
        }
    }

    /// <summary>
    /// Keeps the room present in the gaps: while the assistant is not speaking, this writes the bed on its own so
    /// the line never falls to perfect silence.
    /// </summary>
    /// <remarks>
    /// It defers to the assistant rather than mixing on top of it. The speech path already carries the bed, so
    /// writing here at the same time would both double the room and interleave two writers on one media session.
    /// </remarks>
    private async Task PumpAmbienceAsync(
        IContactCenterVoiceMediaSession media,
        CallAmbience ambience,
        CancellationToken cancellationToken)
    {
        // One frame per tick, generated at the model's rate and converted the same way speech is.
        var samplesPerFrame = RealtimeAudioConverter.RealtimeSampleRate * AmbienceFrameMilliseconds / 1000;
        var frameInterval = TimeSpan.FromMilliseconds(AmbienceFrameMilliseconds);

        try
        {
            using var timer = new PeriodicTimer(frameInterval);

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var sinceAssistant = DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastAssistantAudioTicks);

                // A short hold-off rather than an exact handover: assistant audio arrives in bursts, and treating
                // the gap between two deltas as silence would make the room stutter through every sentence.
                if (sinceAssistant < TimeSpan.TicksPerMillisecond * AssistantSilenceHoldoffMilliseconds)
                {
                    continue;
                }

                var bed = RealtimeAudioConverter.FromRealtime(ambience.NextPcmBytes(samplesPerFrame), media.OutgoingFormat);

                if (!bed.IsEmpty)
                {
                    await media.WriteOutgoingAsync(new ContactCenterVoiceMediaFrame { Data = bed }, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The call ended. Nothing to report.
        }
        catch (Exception ex)
        {
            // The bed is cosmetic. Losing it must never take the call down with it.
            _logger.LogWarning(ex, "The call ambience stopped during a realtime voice session; the call continues without it.");
        }
    }

    private async Task StorePromptAsync(
        RealtimeVoiceConversationContext context,
        ChatRole role,
        string content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _promptStore.CreateAsync(new AIChatSessionPrompt
        {
            ItemId = UniqueId.GenerateId(),
            SessionId = context.Session.SessionId,
            Role = role,
            Content = content.Trim(),

            // Stamp the turn's time. Without this it defaults to DateTime.MinValue, and since the transcript is
            // read back in CreatedUtc order every realtime turn collapses onto the same instant — so the call
            // review is handed a conversation whose speakers are interleaved in arbitrary order, and it
            // dispositions the call off that. A two-minute answered call came back as "No Answer" this way.
            CreatedUtc = now,
        }, cancellationToken);

        context.Session.LastActivityUtc = now;
        await _chatSessionManager.SaveAsync(context.Session, cancellationToken);

        // Commit the turn now. A realtime session holds the call for its entire duration inside a single scope, so
        // without an explicit flush every turn's write sits uncommitted until the call ends and the scope disposes
        // — one write transaction held open for minutes. On SQLite that is a tenant-wide stall: a two-minute call
        // blocked agent presence heartbeats and other webhook deliveries until they timed out. Flushing per turn
        // keeps each write short, and also means a transcript survives a call that ends abruptly.
        await _session.SaveChangesAsync(cancellationToken);
    }

    private static Task Settle(Task task)
        => task.ContinueWith(static _ => { }, TaskScheduler.Default);
}
