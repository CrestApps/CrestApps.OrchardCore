using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
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
    /// When the caller was last heard to say something, so a closing call can tell "they are done" from "they
    /// had one more thing".
    /// </summary>
    private long _lastCallerSpeechTicks;

    /// <summary>
    /// How long the session is given to finish its closing line after the model asks to transfer, before it is
    /// closed and the caller is handed to the queue. Long enough for "connecting you now", short enough that a
    /// caller is never left with the assistant after being promised a person.
    /// </summary>
    private static readonly TimeSpan HandoffClosingGrace = TimeSpan.FromSeconds(4);

    /// <summary>
    /// How long the assistant is given to begin its closing line after asking to end the call.
    /// </summary>
    /// <remarks>
    /// The tool call and the goodbye are one action as far as the model is concerned, and the tool usually lands
    /// first. Without this wait the silence in between reads as "finished speaking" and the goodbye is cut off at
    /// the first word. Bounded, because a model that ends a call without saying anything must still hang up.
    /// </remarks>
    private static readonly TimeSpan ClosingSpeechStartGrace = TimeSpan.FromSeconds(4);

    /// <summary>
    /// How long the caller is left the line after the assistant's goodbye before the call is hung up.
    /// </summary>
    /// <remarks>
    /// Hanging up the instant the closing line ends cuts off the person who was drawing breath to say "actually,
    /// one more thing" — and being hung up on is remembered long after the rest of the call is forgotten. If they
    /// do speak, the assistant answers and the call carries on; this window only ends a conversation that both
    /// sides have finished.
    /// </remarks>
    private static readonly TimeSpan ClosingListeningGrace = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How often the closing watchdog re-checks. Fine enough that the hangup lands when it was meant to.
    /// </summary>
    private static readonly TimeSpan ClosingPollInterval = TimeSpan.FromMilliseconds(150);

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

        // The model saying the conversation is over is not the same as the call being over, which is why this is
        // a watchdog rather than another CancelAfter: it waits for the goodbye to finish and then leaves the line
        // open a moment, and abandons the hangup entirely if the caller uses it.
        var closing = context.EndCallRequested.CanBeCanceled
            ? CloseWhenConversationEndsAsync(callScope, context.EndCallRequested)
            : Task.CompletedTask;

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

            // All of them are awaited so none is left writing to a disposed session.
            await Task.WhenAll(Settle(toModel), Settle(toCaller), Settle(bed), Settle(closing));
            await media.StopAsync(CancellationToken.None);
        }

        return true;
    }

    /// <summary>
    /// Gives the live session the tools a phone call needs, and tells it when to use them.
    /// </summary>
    /// <remarks>
    /// A realtime session is configured once, at the start, from this context — there is no per-turn completion
    /// to hang a tool on the way the turn-based path does. The end-call tool is registered as a scoped system
    /// entry and named in <c>MustIncludeTools</c> so the profile's own tool selection cannot leave it out: every
    /// call has to be endable, whatever else the profile is configured to do.
    /// </remarks>
    /// <param name="context">The orchestration context the session is built from.</param>
    private static void ConfigureCallTools(OrchestrationContext orchestration, RealtimeVoiceConversationContext call)
    {
        if (orchestration?.CompletionContext is null)
        {
            return;
        }

        AttachTool(orchestration, EndCallTool.ToolName, "Ends the phone call once the conversation is over.");

        // Escalation, when this call has somewhere to escalate to. The tool and the guidance travel together:
        // a tool the model is never told about is not used, and guidance without a tool has the model promising
        // a transfer that nothing performs.
        if (!string.IsNullOrWhiteSpace(call?.HandoffInstructions))
        {
            AttachTool(
                orchestration,
                OmnichannelHandoffHelper.TransferToAgentToolName,
                "Transfers the current conversation to a live human agent.");

            orchestration.SystemMessageBuilder.AppendLine();
            orchestration.SystemMessageBuilder.AppendLine(call.HandoffInstructions);
        }

        // Somebody trying to end contact must never be handed to a person instead. This is written for the way it
        // actually arrives: speech recognition on a phone line drops small words, and "don't call me" reaches the
        // model as "call me" — which reads as a request to be connected and was, on a live call, acted on as one.
        // The safe reading of an ambiguous fragment near a refusal is the one that stops calling.
        orchestration.SystemMessageBuilder.AppendLine();
        orchestration.SystemMessageBuilder.AppendLine("## When somebody asks you to stop calling");
        orchestration.SystemMessageBuilder.AppendLine();
        orchestration.SystemMessageBuilder.AppendLine(
            "If the customer asks not to be called, to be taken off the list, or to stop calling, that is an " +
            "opt-out and it ends the call. Acknowledge it plainly, say they will not be contacted again, and end " +
            "the call. Never transfer somebody who is trying to end contact, and never treat it as interest. " +
            "Phone audio drops small words, so a short or garbled phrase around a refusal — including one that " +
            "sounds like an invitation to call — is an opt-out unless the customer clearly says otherwise; if you " +
            "genuinely cannot tell, ask them to confirm rather than assuming the answer that keeps them on the list.");

        // Who picked up. A model opening a sales call with no name does not decline to use one — it invents a
        // plausible one, and the person who answers knows immediately that nobody actually knows them. Observed
        // live: "is this Marcus?" to a contact named Amani, who asked who it was looking for, which the assistant
        // then read as a request for a human and transferred the call.
        orchestration.SystemMessageBuilder.AppendLine();
        orchestration.SystemMessageBuilder.AppendLine("## Who you are calling");
        orchestration.SystemMessageBuilder.AppendLine();
        orchestration.SystemMessageBuilder.AppendLine(
            string.IsNullOrWhiteSpace(call?.ContactName)
                ? "You do not know the name of the person you are calling. Do not use a name, and never guess or " +
                  "invent one; ask who you are speaking with if you need it."
                : $"You are calling {call.ContactName}. That is the only name you may use for them. Never use any " +
                  "other name, and never guess or invent one — if the person says they are somebody else, believe " +
                  "them and adjust.");

        // Said plainly, because the model is speaking rather than writing and cannot see the call state: on a
        // phone call somebody has to hang up, and if it does not, the customer is left holding a dead line.
        orchestration.SystemMessageBuilder.AppendLine();
        orchestration.SystemMessageBuilder.AppendLine("## Ending the call");
        orchestration.SystemMessageBuilder.AppendLine();
        orchestration.SystemMessageBuilder.AppendLine(
            $"You are on a live phone call. When the conversation has genuinely finished — the customer has what " +
            $"they needed, has declined, has asked not to be called again, or has said goodbye — say a short, warm " +
            $"closing line and then call the {EndCallTool.ToolName} tool. The call is hung up for you once you have " +
            $"finished speaking and the customer has had a moment to add anything, so do not announce that you are " +
            $"hanging up and do not wait for them to do it. Never call it while the customer still has questions or " +
            $"is being transferred to a person.");
    }

    /// <summary>
    /// Puts one system tool in front of a realtime session.
    /// </summary>
    /// <remarks>
    /// Registered as a scoped entry (the profile's own tool list skips system tools) and named in
    /// <c>MustIncludeTools</c> so the profile's selection cannot leave it out. Both are idempotent, because this
    /// runs once per call and a duplicate would be offered to the model twice.
    /// </remarks>
    /// <param name="orchestration">The orchestration context the session is built from.</param>
    /// <param name="toolName">The tool's registered name.</param>
    /// <param name="description">What the tool does, for the registry entry.</param>
    private static void AttachTool(OrchestrationContext orchestration, string toolName, string description)
    {
        var scoped = orchestration.CompletionContext.AdditionalProperties
            .TryGetValue(FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey, out var existing) &&
            existing is List<ToolRegistryEntry> entries
                ? entries
                : [];

        if (!scoped.Exists(entry => entry.Id == toolName))
        {
            scoped.Add(new ToolRegistryEntry
            {
                Id = toolName,
                Name = toolName,
                Description = description,
                Source = ToolRegistryEntrySource.System,
                CreateAsync = serviceProvider => ValueTask.FromResult(
                    serviceProvider.GetKeyedService<AITool>(toolName)),
            });
        }

        orchestration.CompletionContext.AdditionalProperties[FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey] = scoped;

        if (!orchestration.MustIncludeTools.Contains(toolName))
        {
            orchestration.MustIncludeTools.Add(toolName);
        }
    }

    /// <summary>
    /// Ends the call once the model has said the conversation is over and the goodbye has actually been said.
    /// </summary>
    /// <remarks>
    /// Three things have to be true before a call is cut, and each was learned by picturing the person holding
    /// the phone. The model has to have decided the conversation is finished. Its closing line has to have
    /// finished playing, or the caller hears "thanks for your ti-" and a dead line. And the caller has to have
    /// been given a breath to say the thing people say after goodbye — if they take it, the hangup is abandoned
    /// altogether and the assistant answers them, because a caller who is still talking has not finished the
    /// call no matter what the model concluded.
    /// </remarks>
    /// <param name="callScope">The scope whose cancellation ends the call.</param>
    /// <param name="endCallRequested">Cancelled when the model reports the conversation finished.</param>
    private async Task CloseWhenConversationEndsAsync(CancellationTokenSource callScope, CancellationToken endCallRequested)
    {
        // Watched together, because a call ends for all sorts of reasons that are nothing to do with this: the
        // caller hangs up, the model asks to transfer, the session fails. Waiting on the end-call signal alone
        // meant that on every one of those calls this task simply never finished -- and the teardown waits for
        // it, so the session never returned and everything after it never ran. That is what left a caller who
        // had just been promised a person listening to nothing: the transfer was recorded and the code that
        // would have seated them in the queue was never reached.
        using var closing = CancellationTokenSource.CreateLinkedTokenSource(endCallRequested, callScope.Token);

        try
        {
            // Wait for the model to say the conversation is over. Nothing below runs on an ordinary call.
            await Task.Delay(Timeout.InfiniteTimeSpan, closing.Token);
        }
        catch (OperationCanceledException) when (endCallRequested.IsCancellationRequested)
        {
            // This is the signal, not a failure.
        }
        catch (OperationCanceledException)
        {
            // The call ended on its own. There is nothing left to close.
            return;
        }

        var requestedAtTicks = DateTime.UtcNow.Ticks;
        var closingLineStarted = false;

        while (!callScope.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(ClosingPollInterval, callScope.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var now = DateTime.UtcNow.Ticks;
            var lastAssistantTicks = Interlocked.Read(ref _lastAssistantAudioTicks);

            // The caller spoke after the model decided it was done. They get the call back: the assistant is
            // already answering them, and hanging up mid-answer would be worse than never having closed at all.
            if (Interlocked.Read(ref _lastCallerSpeechTicks) > requestedAtTicks)
            {
                return;
            }

            closingLineStarted |= lastAssistantTicks > requestedAtTicks;

            // The model usually calls the tool and says its goodbye immediately after, so the silence at this
            // moment is the gap before it starts — not the end of anything. Waiting for it to speak is what keeps
            // the closing line from being cut off at the first word. A model that says nothing at all still has
            // to end the call, so the wait is bounded.
            if (!closingLineStarted && now - requestedAtTicks < ClosingSpeechStartGrace.Ticks)
            {
                continue;
            }

            // Quiet since the goodbye ended — and long enough that the caller has had their moment to answer it.
            // Measured from the assistant's last audio rather than from the tool call, so a long closing line
            // does not eat the window the caller was supposed to get.
            if (now - lastAssistantTicks < ClosingListeningGrace.Ticks)
            {
                continue;
            }

            await callScope.CancelAsync();

            return;
        }
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
                RealtimeDeploymentName = context.RealtimeDeploymentName,
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

                // A realtime session is built from this context rather than from a per-turn completion, so the
                // tools a live call needs — and the instruction to use them — have to be put here. Without it the
                // model has no way to end a call it knows is over.
                ConfigureContext = orchestration => ConfigureCallTools(orchestration, context),
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

                    case RealtimeConversationEventType.UserTranscript:
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
