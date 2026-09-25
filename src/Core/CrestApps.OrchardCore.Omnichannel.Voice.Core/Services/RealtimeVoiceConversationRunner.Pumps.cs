using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Realtime;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
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
    /// Interruption stays on: being talked over is the other half of sounding like a machine.
    /// </para>
    /// <para>
    /// What this can and cannot change depends on the detector the session runs, and that is the tenant's
    /// realtime transport setting, not ours. Under the default (<c>semantic_vad</c>, eagerness <c>auto</c>) the
    /// Core conversation re-sends the semantic detector and <em>ignores</em> the silence and threshold passed here —
    /// semantic detection has neither. They only take effect on a tenant configured for <c>server_vad</c>. So on a
    /// default tenant this call keeps interruption on and nothing else; the phantom turns the comments above
    /// describe are held back by <see cref="CallerEchoGuard"/> before the audio reaches the detector at all.
    /// </para>
    /// </remarks>
    private async Task ApplyTelephonyTurnDetectionAsync(IRealtimeConversation conversation, CancellationToken cancellationToken)
    {
        try
        {
            // The detector type is left as the session already has it: the valid values belong to the provider,
            // and naming one here would be a guess that fails closed on a live call. Core offers no way to set
            // semantic eagerness or input noise reduction from here, which is why neither is tuned.
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
        LiveConversation live,
        RealtimeVoiceConversationContext context,
        AssistantBargeIn bargeIn,
        CancellationToken cancellationToken)
    {
        // The assistant's own voice comes back up the caller's line, and the provider hears it as the caller
        // talking: live, that is how a call ended up answering "Bye-bye." and "you" that nobody said. The guard
        // holds back what can only be that echo while the assistant is speaking, and lets a caller who talks over
        // it straight through. See CallerEchoGuard for why it is shaped the way it is.
        var guard = new CallerEchoGuard();
        var released = new List<ReadOnlyMemory<byte>>(4);
        var openingsLogged = 0;

        try
        {
            await foreach (var frame in media.ReadIncomingAsync(cancellationToken))
            {
                var audio = RealtimeAudioConverter.ToRealtime(frame.Data, media.IncomingFormat);

                if (audio.IsEmpty)
                {
                    continue;
                }

                released.Clear();
                var now = DateTime.UtcNow.Ticks;
                guard.Process(audio, now, Interlocked.Read(ref _assistantSpeechEndsTicks), released);

                // Recorded before the audio goes to the model, so that by the time the provider reports the caller
                // starting, the other pump already knows it was a voice over the assistant and not its echo.
                if (guard.IsLettingCallerThrough)
                {
                    bargeIn.CallerTalkingOver(now);
                }

                await SendToModelAsync(live, released, context, cancellationToken);

                if (guard.Openings > openingsLogged)
                {
                    openingsLogged = guard.Openings;

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        // The level, because an opening on echo rather than a voice is exactly what would show
                        // the barge-in level is set too low for a line.
                        _logger.LogDebug(
                            "The caller on activity '{ActivityId}' spoke over the assistant at {LevelDbfs:F1} dBFS and was let through.",
                            context.Activity?.ItemId.SanitizeLogValue(),
                            guard.LastOpeningDbfs);
                    }
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
        finally
        {
            _meter?.EchoHeld(guard.WithheldMilliseconds);
            LogEchoGuard(guard, context);
        }
    }

    /// <summary>
    /// Sends the caller's audio to whichever session is carrying the call.
    /// </summary>
    /// <remarks>
    /// A send that fails means the session's socket has gone, not the caller: the line is still delivering their
    /// voice. It used to end this pump, and with it the call. Now the session is marked dead so it can be replaced,
    /// and the caller's audio keeps being read. While a replacement opens there is nowhere to send it, so it is not.
    /// </remarks>
    private async Task SendToModelAsync(
        LiveConversation live,
        List<ReadOnlyMemory<byte>> released,
        RealtimeVoiceConversationContext context,
        CancellationToken cancellationToken)
    {
        var conversation = live.Current;

        if (conversation is null || live.Faulted)
        {
            return;
        }

        try
        {
            foreach (var chunk in released)
            {
                await conversation.SendAudioAsync(chunk, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The caller's audio could not be sent to the realtime session on activity '{ActivityId}'; the session is treated as lost.", context.Activity?.ItemId.SanitizeLogValue());
            live.Fault(conversation);
        }
    }

    /// <summary>
    /// Records what the echo guard held back on this call, so its barge-in level can be checked against real
    /// lines rather than guessed at.
    /// </summary>
    private void LogEchoGuard(CallerEchoGuard guard, RealtimeVoiceConversationContext context)
    {
        if (guard.WithheldMilliseconds == 0 || !_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        _logger.LogInformation(
            "The echo guard on activity '{ActivityId}' sent {WithheldMilliseconds} ms of caller audio heard while the assistant was speaking to the model as silence (loudest {LoudestDbfs:F1} dBFS, barge-in level {BargeInDbfs:F1} dBFS) and let the caller talk over the assistant {Openings} time(s).",
            context.Activity?.ItemId.SanitizeLogValue(),
            guard.WithheldMilliseconds,
            guard.LoudestWithheldDbfs,
            CallerEchoGuard.BargeInLevelDbfs,
            guard.Openings);
    }

    // Returns whether the session was lost: its stream ended, failed, or reported an error it cannot continue from,
    // while nothing had asked it to stop. False when it stopped because the call is ending.
    private async Task<bool> PumpAssistantAudioAsync(
        IContactCenterVoiceMediaSession media,
        IRealtimeConversation conversation,
        RealtimeVoiceConversationContext context,
        CallAmbience ambience,
        AssistantBargeIn bargeIn,
        CancellationToken cancellationToken)
    {
        var assistantText = new System.Text.StringBuilder();

        // Errors in a row with nothing else between them. One is a refusal; a run of them is a session that is gone.
        var consecutiveErrors = 0;

        // Set once the assistant is finished talking for good, and cleared the moment the customer speaks again.
        var goodbyeSaid = false;

        // The model finishes speaking its closing line and calls the end-call tool after it, so by the time the
        // request arrives the goodbye has usually already been said. Watching the request rather than reading it
        // when a line ends is what tells the two apart: the line in flight when it arrives is the goodbye and is
        // allowed to finish; anything the model starts afterwards is the repeat.
        var closingRequested = false;
        var utteranceInFlight = false;

        // Whether the assistant has finished a line since the customer last spoke. A request to end the call that
        // arrives with nothing in flight means the goodbye is already said only when the assistant has answered
        // the customer's last words. The model does not always speak first: live, after the customer said "bye",
        // it called the tool and only then said "bye, take care" -- a goodbye this would otherwise have muted.
        var spokeSinceCaller = false;

        // The requests already acted on, so a later one -- the model ending the call again after the customer
        // answered its first goodbye -- is recognised as new.
        var requestsSeen = 0;

        void OnClosingRequested()
        {
            closingRequested = true;

            // Nothing is being said, and the assistant has already answered the customer, so the goodbye is behind
            // us. The closing watchdog is told too, so it does not wait for a goodbye this pump will now suppress.
            if (!Volatile.Read(ref utteranceInFlight) && Volatile.Read(ref spokeSinceCaller))
            {
                goodbyeSaid = true;
                Volatile.Write(ref _goodbyeAlreadySaid, true);
            }
        }

        using var closingRegistration = context.EndCallRequested.CanBeCanceled
            ? context.EndCallRequested.Register(() =>
            {
                Volatile.Write(ref requestsSeen, Math.Max(Volatile.Read(ref requestsSeen), 1));
                OnClosingRequested();
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
                // The token only ever fires for the first request, so later ones are noticed here, on the next event
                // after the tool call -- which is before anything the model says in reply to it.
                var requests = context.EndCallRequests?.Invoke() ?? 0;

                if (requests > Volatile.Read(ref requestsSeen))
                {
                    Volatile.Write(ref requestsSeen, requests);
                    OnClosingRequested();
                }

                consecutiveErrors = conversationEvent.Type == RealtimeConversationEventType.Error ? consecutiveErrors + 1 : 0;

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

                        // The rest of a line the caller talked over. The model had produced more of it than was
                        // queued when they started, and playing it now would restart the assistant mid-sentence
                        // on a line that had just been cleared for them.
                        if (!bargeIn.ShouldPlay(conversationEvent.ResponseId, conversationEvent.ItemId))
                        {
                            break;
                        }

                        utteranceInFlight = true;

                        if (!speech.IsEmpty)
                        {
                            // Stamped for two readers: the bed pump, which must stay quiet while the assistant is
                            // talking or the room doubles, and the closing watchdog, which waits for the goodbye
                            // to actually finish before it hangs up. Both mean "finished playing", not "arrived".
                            // Where it plays is kept too, so a caller who talks over it can have it taken back.
                            var startsTicks = ExtendAssistantPlayback(speech.Length);
                            bargeIn.Queued(conversationEvent.ResponseId, conversationEvent.ItemId, startsTicks, speech.Length, DateTime.UtcNow.Ticks);
                            _meter?.AssistantAudioScheduled(startsTicks, AssistantBargeIn.DurationTicks(speech.Length));
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
                        _meter?.CallerSpeechStarted(DateTime.UtcNow.Ticks);

                        // First, while it is still known whether the line being talked over is the closing one:
                        // a caller talking over the assistant has to stop hearing it, and the provider stopping
                        // the model does not do that on its own.
                        await StopSpeakingWhenTalkedOverAsync(
                            media,
                            conversation,
                            bargeIn,
                            closing: closingRequested || goodbyeSaid || context.HandoffRequested.IsCancellationRequested,
                            activityId,
                            cancellationToken);

                        // They are talking, so the call is not over after all and the assistant may answer.
                        goodbyeSaid = false;
                        closingRequested = false;
                        Volatile.Write(ref spokeSinceCaller, false);
                        Volatile.Write(ref _goodbyeAlreadySaid, false);

                        // The first syllable, not the finished sentence. A transcript only exists once the caller
                        // has stopped talking and the provider has transcribed them, which is seconds later --
                        // long enough that a call closing down would already have been cut. This is the moment
                        // the caller decides the conversation is not over, so it is the moment that has to count.
                        Interlocked.Exchange(ref _lastCallerSpeechTicks, DateTime.UtcNow.Ticks);

                        break;

                    case RealtimeConversationEventType.ResponseStarted:
                    case RealtimeConversationEventType.UserTurnCommitted:
                        // The detector commits the caller's turn once it hears them stop, which is the only end of
                        // their speech the session reports.
                        if (conversationEvent.Type == RealtimeConversationEventType.UserTurnCommitted)
                        {
                            _meter?.CallerSpeechStopped(DateTime.UtcNow.Ticks);
                        }

                        // A new turn, so speech from here is not the rest of a line the caller talked over.
                        bargeIn.NextTurn();

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
                        Volatile.Write(ref spokeSinceCaller, true);

                        // The line that was in flight when the call was closed is the goodbye. It has now been
                        // said, so the assistant is done talking.
                        goodbyeSaid = closingRequested;

                        break;

                    case RealtimeConversationEventType.Error:
                        // Most provider errors are a refusal of one request and leave the session as it was. Live,
                        // treating every one as the end of the call turned a refused truncation into dead air.
                        if (!await RideOutErrorAsync(conversation, bargeIn, conversationEvent.ErrorMessage, consecutiveErrors, activityId, cancellationToken))
                        {
                            return true;
                        }

                        break;
                }
            }

            // The stream ended with nothing having asked it to: the provider closed the session.
            return !cancellationToken.IsCancellationRequested;
        }
        catch (OperationCanceledException)
        {
            // The call ended, or the session was found dead elsewhere. The caller decides which.
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The realtime voice session on activity '{ActivityId}' ended unexpectedly.", activityId);

            return !cancellationToken.IsCancellationRequested;
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

        var now = _clock.UtcNow;

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
