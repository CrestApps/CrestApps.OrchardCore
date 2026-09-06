using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Realtime;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Holds an automated phone conversation as a live speech-to-speech session: caller audio goes to the model as it
/// arrives, and the model's voice goes back to the caller as it is produced.
/// </summary>
public sealed class RealtimeVoiceConversationRunner : IRealtimeVoiceConversationRunner
{
    private readonly IRealtimeOrchestrator _orchestrator;
    private readonly IContactCenterVoiceMediaProviderResolver _mediaResolver;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// The length of one ambience frame, in milliseconds. Matches the packet size a phone call is carried in.
    /// </summary>
    private const int AmbienceFrameMilliseconds = 20;

    /// <summary>
    /// How long after the last assistant audio the bed waits before filling the silence itself.
    /// </summary>
    private const int AssistantSilenceHoldoffMilliseconds = 200;

    /// <summary>
    /// When the assistant last wrote audio, so the bed can stay out of its way.
    /// </summary>
    private long _lastAssistantAudioTicks;

    /// <summary>
    /// How long the session is given to finish its closing line after the model asks to transfer, before it is
    /// closed and the caller is handed to the queue. Long enough for "connecting you now", short enough that a
    /// caller is never left with the assistant after being promised a person.
    /// </summary>
    private static readonly TimeSpan HandoffClosingGrace = TimeSpan.FromSeconds(4);

    /// <summary>
    /// How long the caller must be quiet before the session treats their turn as finished. Longer than the
    /// provider default, because a person on the phone pauses mid-sentence and a call carries noise through those
    /// pauses; cutting in on them is what makes an assistant feel like it is not listening.
    /// </summary>
    private const int TelephonySilenceDurationMilliseconds = 900;

    /// <summary>
    /// How confident the detector must be that it is hearing speech.
    /// </summary>
    /// <remarks>
    /// Left at roughly the provider default on purpose. Raising it to 0.62 to suppress phantom turns did suppress
    /// them, and also clipped the onset of short quiet answers: a caller who replied "yeah" had it reach the model
    /// as a fragment, which came back transcribed as "That's causing a fever somewhere." The assistant read that
    /// as a brush-off and politely ended the call on somebody who had just agreed to talk.
    /// <para>
    /// A short affirmative is the most common thing a caller says, so mis-hearing it is far worse than the
    /// phantom turns the higher threshold was buying. Waiting longer for a pause — which is a separate setting —
    /// is the part that stops the assistant talking over people, and it does not cost anything at the onset.
    /// </para>
    /// </remarks>
    private const float TelephonyVadThreshold = 0.5f;

    /// <summary>
    /// Initializes a new instance of the <see cref="RealtimeVoiceConversationRunner"/> class.
    /// </summary>
    /// <param name="orchestrator">The realtime orchestrator, which owns tools, the system prompt and data sources.</param>
    /// <param name="mediaResolver">The resolver for the provider that can carry live audio on this call.</param>
    /// <param name="promptStore">The transcript store.</param>
    /// <param name="chatSessionManager">The chat session manager.</param>
    /// <param name="session">The document session, flushed after each turn so the call does not hold a write transaction open.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public RealtimeVoiceConversationRunner(
        IRealtimeOrchestrator orchestrator,
        IContactCenterVoiceMediaProviderResolver mediaResolver,
        IAIChatSessionPromptStore promptStore,
        IAIChatSessionManager chatSessionManager,
        ISession session,
        IClock clock,
        ILogger<RealtimeVoiceConversationRunner> logger)
    {
        _orchestrator = orchestrator;
        _mediaResolver = mediaResolver;
        _promptStore = promptStore;
        _chatSessionManager = chatSessionManager;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> RunAsync(RealtimeVoiceConversationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Profile is null || context.Session is null || string.IsNullOrEmpty(context.ProviderCallId))
        {
            return false;
        }

        var mediaProvider = _mediaResolver.Get(context.ProviderName);

        // No live media means no realtime: the model needs the caller's audio, not a transcript of it. Reporting
        // that here lets the caller fall back to the turn-based loop instead of sitting on a silent call.
        if (mediaProvider is null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Provider '{Provider}' cannot carry live media, so activity '{ActivityId}' runs the turn-based voice loop instead of a realtime session.",
                    context.ProviderName.SanitizeLogValue(),
                    context.Activity?.ItemId.SanitizeLogValue());
            }

            return false;
        }

        await using var media = await mediaProvider.OpenSessionAsync(new ContactCenterVoiceMediaSessionRequest
        {
            ProviderCallId = context.ProviderCallId,
            InteractionId = context.InteractionId,
        }, cancellationToken);

        await using var conversation = await StartConversationAsync(context, cancellationToken);

        if (conversation is null)
        {
            return false;
        }

        await ApplyTelephonyTurnDetectionAsync(conversation, cancellationToken);

        using var callScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // The moment the model asks to transfer, the caller stops being the assistant's to talk to. Without this
        // the session ran until the caller hung up — the transfer was recorded, the caller was told someone was
        // coming, and then the assistant kept right on chatting, with the enqueue only happening when the call
        // would have ended anyway. Observed live at 19 seconds on one call and 40 on the next.
        //
        // It is a short grace rather than an immediate cut: the model announces the transfer in the same breath
        // as it invokes the tool ("connecting you with an agent right now"), so cutting the audio the instant the
        // tool fires would clip that line mid-word and drop the caller into silence with no idea what happened.
        using var handoffRegistration = context.HandoffRequested.CanBeCanceled
            ? context.HandoffRequested.Register(() => callScope.CancelAfter(HandoffClosingGrace))
            : default;

        // One generator drives both paths, at the rate the model speaks: the bed is mixed under the assistant's
        // own audio while it talks, and written on its own while it does not, so the room never cuts in and out.
        var ambience = context.UseCallAmbience
            ? new CallAmbience(RealtimeAudioConverter.RealtimeSampleRate)
            : null;

        // Both directions run at once. That is the entire point: a turn-based loop cannot answer until the caller
        // has finished, and this one starts answering while they are still talking.
        var toModel = PumpCallerAudioAsync(media, conversation, callScope.Token);
        var toCaller = PumpAssistantAudioAsync(media, conversation, context, ambience, callScope.Token);
        var bed = ambience is null
            ? Task.CompletedTask
            : PumpAmbienceAsync(media, ambience, callScope.Token);

        try
        {
            // Whichever side ends first ends the call: the caller hung up, or the session closed. The bed is not
            // one of them — it never ends on its own, and a call must not be held open by it.
            await Task.WhenAny(toModel, toCaller);
        }
        finally
        {
            await callScope.CancelAsync();

            // All three are awaited so none is left writing to a disposed session.
            await Task.WhenAll(Settle(toModel), Settle(toCaller), Settle(bed));
            await media.StopAsync(CancellationToken.None);
        }

        return true;
    }

    private async Task<IRealtimeConversation> StartConversationAsync(
        RealtimeVoiceConversationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _orchestrator.StartAsync(new RealtimeOrchestrationRequest
            {
                Resource = context.Profile,
                RealtimeDeploymentName = context.Profile.RealtimeDeploymentName,
                ChatSession = context.Session,

                // The voice the activity was loaded with, so a realtime call sounds like the campaign it belongs
                // to rather than like the model's default.
                // The campaign's voice when the inventory load chose one, otherwise the voice configured on the
                // profile itself. Only the activity was read before, and a batch does not set a voice unless
                // somebody picks one — so the voice an operator selected on the profile was silently ignored and
                // every realtime call used the model's default, whatever the profile said.
                Voice = ResolveVoice(context),

                // A caller who talks over the assistant is interrupting a person as far as they are concerned,
                // and being talked through is the single most common complaint about automated calls.
                AllowInterruption = true,
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "A realtime voice session could not be started for activity '{ActivityId}'.", context.Activity?.ItemId.SanitizeLogValue());

            return null;
        }
    }

    /// <summary>
    /// The voice this call should be spoken in: the one the activity was loaded with, falling back to the one
    /// configured on the profile.
    /// </summary>
    /// <remarks>
    /// The activity only carries a voice when the inventory load explicitly chose one, which is the exception
    /// rather than the rule. Reading only the activity therefore threw away the profile's own setting, so an
    /// operator who picked a voice there heard the model's default on every call and had no way to tell why.
    /// </remarks>
    /// <param name="context">The call being held.</param>
    private static string ResolveVoice(RealtimeVoiceConversationContext context)
    {
        var activityVoice = context.Activity?.TextToSpeechVoiceId;

        if (!string.IsNullOrWhiteSpace(activityVoice))
        {
            return activityVoice.Trim();
        }

        if (context.Profile is not null &&
            context.Profile.TryGetSettings<ChatModeProfileSettings>(out var settings) &&
            !string.IsNullOrWhiteSpace(settings.VoiceName))
        {
            return settings.VoiceName.Trim();
        }

        // Nothing chosen anywhere: let the deployment use whatever it defaults to.
        return null;
    }

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

        try
        {
            await foreach (var conversationEvent in conversation.GetEventsAsync(cancellationToken))
            {
                switch (conversationEvent.Type)
                {
                    case RealtimeConversationEventType.AssistantAudioDelta:
                        var speech = conversationEvent.Audio;

                        if (ambience is not null && !speech.IsEmpty)
                        {
                            // Mixed before conversion so the bed is resampled and companded with the voice, and
                            // arrives shaped by the same line as the speech rather than sitting on top of it.
                            var mixed = speech.ToArray();
                            ambience.MixIntoPcmBytes(mixed);
                            speech = mixed;

                            // The bed pump must stay quiet while this is playing, or the room doubles.
                            Interlocked.Exchange(ref _lastAssistantAudioTicks, DateTime.UtcNow.Ticks);
                        }

                        var audio = RealtimeAudioConverter.FromRealtime(speech, media.OutgoingFormat);

                        if (!audio.IsEmpty)
                        {
                            await media.WriteOutgoingAsync(new ContactCenterVoiceMediaFrame { Data = audio }, cancellationToken);
                        }

                        break;

                    case RealtimeConversationEventType.UserTranscript:
                        // Recorded so the call is concluded, summarized and dispositioned exactly the way a
                        // turn-based one is: everything downstream reads the transcript, not the audio.
                        await StorePromptAsync(context, ChatRole.User, conversationEvent.Text, cancellationToken);

                        break;

                    case RealtimeConversationEventType.AssistantTranscriptDelta:
                        assistantText.Append(conversationEvent.Text);

                        break;

                    case RealtimeConversationEventType.AssistantTranscriptDone:
                        var spoken = assistantText.Length > 0 ? assistantText.ToString() : conversationEvent.Text;
                        assistantText.Clear();

                        await StorePromptAsync(context, ChatRole.Assistant, spoken, cancellationToken);

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
