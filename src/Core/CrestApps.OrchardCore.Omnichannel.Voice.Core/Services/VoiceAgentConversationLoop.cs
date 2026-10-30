using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Guardrails;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
using CrestApps.OrchardCore.Telephony.Services;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Liquid;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Drives an automated AI voice conversation over a call, whichever provider is carrying it. The turn loop is a
/// state machine driven purely by provider events and the stored chat transcript, so no per-call server state is
/// required: answered -> speak the greeting; speech ended -> listen (start transcription); a final transcript ->
/// stop listening, run the LLM, speak the reply; hangup -> summarize, disposition, and run the subject actions.
/// <para>
/// None of that is provider-specific, which is why it does not live in a provider module. Everything a provider
/// contributes is behind <see cref="IVoiceAgentMediaProvider"/>: a second provider offers automated voice by
/// implementing a handful of methods rather than by copying this class.
/// </para>
/// </summary>
public sealed partial class VoiceAgentConversationLoop : IVoiceAgentConversationLoop
{
    // Marker the model appends to its final line when it wants to end the call. It is spoken-stripped, but kept
    // in the stored transcript so the speak.ended handler can hang up gracefully after the goodbye finishes.
    internal const string HangupMarker = "[[HANGUP]]";


    private readonly IOmnichannelActivityStore _activityStore;
    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAICompletionService _completionService;
    private readonly IOmnichannelHandoffTurn _handoffTurn;
    private readonly IVoiceCallEndTurn _endCallTurn;
    private readonly IAnsweringMachineSignals _answeringMachineSignals;
    private readonly IRealtimeCallCompletionRunner _completionRunner;

    // Optional: automated voice runs on tenants with no Contact Center, which have no queue to release.
    private readonly IEnumerable<IQueuedCallerAbandonmentHandler> _abandonmentHandlers;
    private readonly IEnumerable<IAutomatedVoiceCallObserver> _callObservers;

    // Optional: registered only while Contact Center call recording is enabled.
    private readonly IEnumerable<IRecordingDisclosureProvider> _disclosureProviders;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIDeploymentCapabilityService _capabilityService;
    private readonly IAICompletionContextBuilder _contextBuilder;
    private readonly IAIProfileManager _profileManager;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly IEnumerable<IOmnichannelHandoffService> _handoffServices;
    private readonly IVoiceAgentMediaProviderResolver _mediaResolver;
    private readonly IRealtimeVoiceConversationRunner _realtimeRunner;
    private readonly ITurnBasedSilenceWatchdog _silenceWatchdog;
    private readonly ITurnBasedCallerKeys _callerKeys;
    private readonly IAIVoiceSessionTracker _sessionTracker;
    private readonly IAIVoiceCallShutdown _callShutdown;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IToolRegistry _toolRegistry;
    private readonly IOmnichannelConversationContextFactory _conversationContextFactory;
    private readonly IAIVoiceCallCommands _voiceCallCommands;
    private readonly IAIConversationTurnNotifier _turnNotifier;
    private readonly IContentManager _contentManager;
    private readonly AIGuardrailService _guardrails;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public VoiceAgentConversationLoop(
        IOmnichannelActivityStore activityStore,
        IAIChatSessionManager chatSessionManager,
        IAIChatSessionPromptStore promptStore,
        IAICompletionService completionService,
        IOmnichannelHandoffTurn handoffTurn,
        IVoiceCallEndTurn endCallTurn,
        IAnsweringMachineSignals answeringMachineSignals,
        IRealtimeCallCompletionRunner completionRunner,
        IEnumerable<IQueuedCallerAbandonmentHandler> abandonmentHandlers,
        IEnumerable<IAutomatedVoiceCallObserver> callObservers,
        IEnumerable<IRecordingDisclosureProvider> disclosureProviders,
        IAIDeploymentManager deploymentManager,
        IAIDeploymentCapabilityService capabilityService,
        IAICompletionContextBuilder contextBuilder,
        IAIProfileManager profileManager,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IEnumerable<IOmnichannelHandoffService> handoffServices,
        IVoiceAgentMediaProviderResolver mediaResolver,
        IRealtimeVoiceConversationRunner realtimeRunner,
        ITurnBasedSilenceWatchdog silenceWatchdog,
        ITurnBasedCallerKeys callerKeys,
        IAIVoiceSessionTracker sessionTracker,
        IAIVoiceCallShutdown callShutdown,
        ILiquidTemplateManager liquidTemplateManager,
        IToolRegistry toolRegistry,
        IOmnichannelConversationContextFactory conversationContextFactory,
        IAIVoiceCallCommands voiceCallCommands,
        IAIConversationTurnNotifier turnNotifier,
        AIGuardrailService guardrails,
        IContentManager contentManager,
        IClock clock,
        ILogger<VoiceAgentConversationLoop> logger,
        IStringLocalizer<VoiceAgentConversationLoop> stringLocalizer)
    {
        _activityStore = activityStore;
        _chatSessionManager = chatSessionManager;
        _promptStore = promptStore;
        _completionService = completionService;
        _handoffTurn = handoffTurn;
        _endCallTurn = endCallTurn;
        _answeringMachineSignals = answeringMachineSignals;
        _completionRunner = completionRunner;
        _abandonmentHandlers = abandonmentHandlers;
        _callObservers = callObservers;
        _disclosureProviders = disclosureProviders;
        _deploymentManager = deploymentManager;
        _capabilityService = capabilityService;
        _contextBuilder = contextBuilder;
        _profileManager = profileManager;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _handoffServices = handoffServices;
        _mediaResolver = mediaResolver;
        _realtimeRunner = realtimeRunner;
        _silenceWatchdog = silenceWatchdog;
        _callerKeys = callerKeys;
        _sessionTracker = sessionTracker;
        _callShutdown = callShutdown;
        _liquidTemplateManager = liquidTemplateManager;
        _toolRegistry = toolRegistry;
        _conversationContextFactory = conversationContextFactory;
        _voiceCallCommands = voiceCallCommands;
        _turnNotifier = turnNotifier;
        _guardrails = guardrails;
        _contentManager = contentManager;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task HandleAsync(VoiceAgentEvent voiceEvent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(voiceEvent?.ActivityId) || string.IsNullOrWhiteSpace(voiceEvent.ProviderCallId))
        {
            return;
        }

        // A call whose provider has no automated-voice media cannot be spoken to at all. Stopping here leaves the
        // call to the provider's own handling rather than running a conversation nobody can hear.
        var media = _mediaResolver.Get(voiceEvent.ProviderName) ?? _mediaResolver.GetDefault();

        if (media is null)
        {
            _logger.LogWarning(
                "No automated voice media is registered for provider '{Provider}'; ignoring the '{EventKind}' event of AI voice activity '{ActivityId}'.",
                voiceEvent.ProviderName.SanitizeLogValue(),
                voiceEvent.Kind,
                voiceEvent.ActivityId.SanitizeLogValue());

            return;
        }

        try
        {
            // A warm or screened transfer's side call, or the caller holding for one.
            if (await HandleAttendedTransferEventAsync(voiceEvent, media, cancellationToken))
            {
                return;
            }

            switch (voiceEvent.Kind)
            {
                case VoiceAgentEventKind.Answered:
                    // Reported before the conversation starts: a realtime session holds the call inside the handler.
                    await ObserveAsync(voiceEvent, AutomatedVoiceCallObservationKind.Answered, cancellationToken: cancellationToken);
                    await OnAnsweredAsync(voiceEvent, media, cancellationToken);
                    break;
                case VoiceAgentEventKind.SpeechStarted:
                    MeterTurnBasedSpeech(voiceEvent, started: true);
                    break;
                case VoiceAgentEventKind.SpeechEnded:
                    MeterTurnBasedSpeech(voiceEvent, started: false);
                    await OnSpeakEndedAsync(voiceEvent, media, cancellationToken);
                    break;
                case VoiceAgentEventKind.Transcription:
                    await OnTranscriptionAsync(voiceEvent, media, cancellationToken);
                    break;
                case VoiceAgentEventKind.Hangup:
                    await OnHangupAsync(voiceEvent, cancellationToken);
                    break;
                case VoiceAgentEventKind.DigitsReceived:
                    await OnDigitsReceivedAsync(voiceEvent);
                    break;
                case VoiceAgentEventKind.AnswererDetected:
                    await ObserveAsync(voiceEvent, AutomatedVoiceCallObservationKind.AnswererDetected, cancellationToken: cancellationToken);
                    await OnAnswererDetectedAsync(voiceEvent, cancellationToken);
                    break;
                case VoiceAgentEventKind.MachineGreetingEnded:
                    await OnMachineGreetingEndedAsync(voiceEvent, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred handling the '{EventKind}' event of AI voice activity '{ActivityId}'.", voiceEvent.Kind, voiceEvent.ActivityId.SanitizeLogValue());
        }
    }

    private async Task OnAnsweredAsync(VoiceAgentEvent voiceEvent, IVoiceAgentMediaProvider media, CancellationToken cancellationToken)
    {
        var activity = await _activityStore.FindByIdAsync(voiceEvent.ActivityId, cancellationToken);

        if (activity is null)
        {
            return;
        }

        var (profile, session) = await ResolveConversationAsync(activity, cancellationToken);

        if (profile is null || session is null)
        {
            _logger.LogWarning("AI voice call answered but no profile/session for activity '{ActivityId}'.", voiceEvent.ActivityId.SanitizeLogValue());
            await media.HangupAsync(voiceEvent.ProviderCallId, cancellationToken);
            return;
        }

        var existingPrompts = await _promptStore.GetPromptsAsync(session.SessionId);

        // Redelivery guard: the greeting is spoken once.
        if (existingPrompts.Any(p => p.Role == ChatRole.Assistant))
        {
            return;
        }

        // The customer answered: advance out of AwaitingCustomerAnswer before anything long-running starts. A
        // realtime session holds the call for its whole duration, and leaving the activity in the awaiting state
        // for that long lets the no-response expiry pass fail a call that is being held right now.
        //
        // Which engine holds the call is decided first, so the activity records it with the call: a supervisor's
        // request is carried out differently for a live session than for the turn-based loop.
        var realtimeDeploymentName = await ResolveRealtimeDeploymentNameAsync(profile, cancellationToken);

        await MarkInProgressAsync(activity, voiceEvent, isRealtime: !string.IsNullOrWhiteSpace(realtimeDeploymentName), cancellationToken);

        // Whatever else the assistant says, a tenant that tells callers they are recorded has it say so first.
        var recordingDisclosure = await ResolveRecordingDisclosureAsync(cancellationToken);

        // A profile whose model can hold a live conversation takes the call as a speech-to-speech session instead
        // of the transcribe-complete-synthesize loop below. That loop cannot begin a reply until the caller has
        // stopped talking, the transcript has come back, the model has answered and the answer has been
        // synthesized; a realtime session answers while they are still finishing, and hears them if they
        // interrupt. Everything downstream is unchanged, because both write the same transcript.
        //
        // Realtime is a capability of the chat deployment now rather than a deployment of its own, so the
        // question is no longer "was a second deployment configured" but "can the one this profile already uses
        // do it".
        if (!string.IsNullOrWhiteSpace(realtimeDeploymentName))
        {
            // The transfer tool records the model's escalation on this turn rather than performing it, so the
            // flag has to start clean for the session we are about to hold. The end-call tool works the same way.
            _handoffTurn.Reset();
            _endCallTurn.Reset();

            // Resolved before the session starts, because a realtime session is configured once and never again:
            // the guidance about when to escalate, and the tool that does it, have to be in place before the
            // caller says a word. The turn-based loop below re-reads this on every turn instead.
            var (realtimeHandoffService, realtimeFlowSettings) = await ResolveVoiceHandoffAsync(activity, cancellationToken);
            var (transferOffered, handoffGuidance) = await OfferHandoffAsync(realtimeHandoffService, realtimeFlowSettings, cancellationToken);

            // The provider's word on who answered arrives on a webhook request of its own while this one holds the
            // call, so it reaches the session through these signals rather than through the activity it also marks.
            // Watched from here, so a report that beats the session to it is still seen.
            var answeringMachine = _answeringMachineSignals.Watch(voiceEvent.ProviderName, voiceEvent.ProviderCallId);

            var sessionHeldTheCall = false;
            var meter = new AIVoiceSessionMeter(measuresCallerSpeech: true);
            RealtimeVoiceConversationContext realtime = null;

            // Held until the call has been handed on below, so a node that starts shutting down waits for this
            // caller to be passed to somebody before it goes.
            using var heldCall = _callShutdown.HoldCall();

            // A supervisor taking the call over ends the session the way the transfer tool does, and the handoff
            // below then rings that supervisor instead of the subject's queue (see AITakeOverRequest).
            using var takeOverRegistration = _voiceCallCommands.Register(activity.ItemId, command =>
            {
                if (command.Kind != AIVoiceCallCommandKind.TakeOver)
                {
                    return Task.FromResult(false);
                }

                _handoffTurn.RequestHandoff(SupervisorTookOverReason);

                return Task.FromResult(true);
            });

            try
            {
                sessionHeldTheCall = await _realtimeRunner.RunAsync(realtime = new RealtimeVoiceConversationContext
                {
                    Activity = activity,
                    Profile = profile,
                    Session = session,
                    ProviderName = voiceEvent.ProviderName,
                    ProviderCallId = voiceEvent.ProviderCallId,

                    // Snapshotted onto the activity when the inventory was loaded, so the campaign that chose the
                    // voice also chose whether the call sounds like someone is sitting in a room.
                    UseCallAmbience = activity.UseCallAmbience,

                    // Ends the session as soon as the transfer tool fires, so the handoff below happens while the
                    // caller is still expecting it rather than whenever the call would otherwise have ended.
                    HandoffRequested = _handoffTurn.HandoffRequestedToken,

                    // Ends the session once the model says the conversation is finished, so the platform hangs up
                    // rather than leaving the customer to notice that nobody is going to.
                    EndCallRequested = _endCallTurn.EndCallRequestedToken,

                    // Read when the call is being closed, since the model only says so as it ends the call.
                    ReachedVoicemail = () => _endCallTurn.ReachedVoicemail,

                    // Every request, not only the first: a customer who answers the goodbye gets the call back,
                    // and the model ending it again afterwards must still hang up.
                    EndCallRequests = () => _endCallTurn.RequestCount,

                    // So a question the assistant asked, or a read-back it announced, keeps the call open until the
                    // customer has answered it, however eager the model is to finish.
                    AssistantSaid = _endCallTurn.AssistantSaid,
                    CustomerAnswered = _endCallTurn.CustomerAnswered,

                    // A voicemail or answering machine answering, as the provider reports it, and the end of its
                    // greeting: the session stops conversing on the first and leaves its message after the second.
                    AnsweredByMachine = answeringMachine.MachineDetected,
                    MachineGreetingEnded = answeringMachine.GreetingEnded,
                    PhoneMenuReached = answeringMachine.PhoneMenuReached,

                    // Ends the call the way the end-call tool does on voicemail once the message has been left, so
                    // the line is hung up as soon as the message has played, and the call concluded as one that
                    // reached voicemail.
                    EndCallOnVoicemail = () => _endCallTurn.RequestEndCall(VoicemailMessageLeftReason, reachedVoicemail: true),
                    EndCallOnSilence = () => _endCallTurn.RequestEndCall(SilentLineEndedReason),

                    // Only when this call actually has an agent to reach, now. Telling a model it may transfer, on a
                    // call where nothing can receive the caller, promises the caller a person who is not coming; a
                    // closed queue is told to the model instead (see OfferHandoffAsync).
                    HandoffInstructions = transferOffered ? handoffGuidance : null,
                    NoHandoffInstructions = transferOffered ? null : handoffGuidance,

                    // The deployment the live session is held on, resolved above by capability.
                    RealtimeDeploymentName = realtimeDeploymentName,

                    // So the assistant addresses the person it actually called, rather than a name it invented.
                    ContactName = await ResolveContactNameAsync(activity, cancellationToken),

                    // A live session writes its own opening, so it is told to give the disclosure word for word.
                    RecordingDisclosure = recordingDisclosure,

                    // Talk time, silence and interruptions, measured from the session's own audio.
                    Meter = meter,

                    // The session lives in this process, so it ends when the node begins to stop, and the caller is
                    // handed on below as from any lost session.
                    NodeStopping = _callShutdown.Stopping,
                }, cancellationToken);

                // The session is over. Said plainly on the record, because the failure this instrumentation was
                // added for looked exactly like the session never ending. Only when there was a session: a
                // provider that cannot carry live media reports that here, and never held one.
                if (sessionHeldTheCall && _logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "The realtime session for activity '{ActivityId}' ended.",
                        activity.ItemId.SanitizeLogValue());
                }
            }
            finally
            {
                // In a finally because what the caller was promised does not depend on how the session ended. A
                // session that fails on its way out -- a media socket closing badly, a provider connection torn
                // down with the request -- still fails after the model has told the caller a person is coming and
                // the tool has recorded it. Leaving that to the success path put the caller on an open, silent
                // line with nothing queued and nobody coming.
                await FinishTheCallElsewhereAsync(activity, voiceEvent, sessionLost: realtime?.SessionLost == true, handoffAnnounced: realtime?.HandoffAnnounced == true);
                await RecordRealtimeSessionAsync(voiceEvent, meter, realtimeDeploymentName, sessionHeldTheCall);

                // The session was only told to give the disclosure, so what it actually said is checked. A call the
                // session never held goes to the turn-based loop below, which gives it itself.
                if (sessionHeldTheCall && !string.IsNullOrWhiteSpace(recordingDisclosure))
                {
                    await VerifyRealtimeRecordingDisclosureAsync(voiceEvent, session.SessionId, recordingDisclosure);
                }

                // Nothing is watching for the provider's report any more.
                _answeringMachineSignals.Forget(voiceEvent.ProviderName, voiceEvent.ProviderCallId);
            }

            if (sessionHeldTheCall)
            {
                return;
            }
        }

        _sessionTracker.BeginTurnBased(activity.ItemId, voiceEvent.OccurredUtc ?? _clock.UtcNow);

        var greeting = await RenderInitialPromptAsync(activity, profile, session, cancellationToken);

        if (string.IsNullOrWhiteSpace(greeting))
        {
            greeting = S["Hi there, do you have a quick minute?"].Value;
        }

        // Spoken by the platform as part of the opening line, so it is said word for word and before anything else.
        // Stored with the greeting, so the model knows the caller has already been told.
        if (!string.IsNullOrWhiteSpace(recordingDisclosure))
        {
            greeting = recordingDisclosure + " " + greeting.Trim();
        }

        await StorePromptAsync(session, ChatRole.Assistant, greeting, cancellationToken);
        var spoken = await SpeakAsync(media, voiceEvent.ProviderCallId, activity, greeting, cancellationToken);

        // The platform says the disclosure itself here, so the provider accepting the line is what gives it.
        if (!string.IsNullOrWhiteSpace(recordingDisclosure))
        {
            await ObserveRecordingDisclosureAsync(voiceEvent, recordingDisclosure, spoken ? null : greeting, spoken, _clock.UtcNow);
        }
    }

    /// <summary>
    /// The recording disclosure the assistant gives first, or <see langword="null"/> when the tenant gives none on
    /// automated voice calls.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private async Task<string> ResolveRecordingDisclosureAsync(CancellationToken cancellationToken)
    {
        foreach (var provider in _disclosureProviders)
        {
            var disclosure = await provider.GetDisclosureAsync(RecordingDisclosureCallType.AIVoiceAgent, cancellationToken);

            if (!string.IsNullOrWhiteSpace(disclosure))
            {
                return disclosure.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// The name of the person being called, or <see langword="null"/> when the contact has none.
    /// </summary>
    /// <param name="activity">The call's activity.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private async Task<string> ResolveContactNameAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activity?.ContactContentItemId))
        {
            return null;
        }

        var contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

        return string.IsNullOrWhiteSpace(contact?.DisplayText) ? null : contact.DisplayText.Trim();
    }

    /// <summary>
    /// Moves an answered call into the live in-progress state.
    /// </summary>
    /// <remarks>
    /// This both records the correct status and takes the activity out of the automated no-response expiry pass
    /// window — that pass only transitions rows still awaiting an answer — so it cannot race the conclusion and
    /// flip a live, answered call to Failed while somebody is still on it.
    /// </remarks>
    private async Task MarkInProgressAsync(OmnichannelActivity activity, VoiceAgentEvent voiceEvent, bool isRealtime, CancellationToken cancellationToken)
    {
        if (activity.Status == ActivityStatus.InProgress && activity.Has<AILiveCall>())
        {
            return;
        }

        // The provider call is recorded with the call, so a supervisor can watch it, take it over or end it.
        activity.Put(new AILiveCall
        {
            ProviderName = voiceEvent.ProviderName,
            ProviderCallId = voiceEvent.ProviderCallId,
            AnsweredUtc = _clock.UtcNow,
            IsRealtime = isRealtime,
        });

        activity.Status = ActivityStatus.InProgress;
        await _activityStore.UpdateAsync(activity, cancellationToken);
    }

    private async Task OnSpeakEndedAsync(VoiceAgentEvent voiceEvent, IVoiceAgentMediaProvider media, CancellationToken cancellationToken)
    {
        // A filler line spoken while a tool was running ended; the reply that follows ends the turn.
        if (TurnBasedToolFillers.TryClaim(voiceEvent.ProviderCallId, _clock.UtcNow))
        {
            return;
        }

        var activity = await _activityStore.FindByIdAsync(voiceEvent.ActivityId, cancellationToken);

        if (activity is null || string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            return;
        }

        var prompts = await _promptStore.GetPromptsAsync(activity.AISessionId);
        var lastAssistant = prompts.LastOrDefault(p => p.Role == ChatRole.Assistant);

        // The model invoked the transfer tool this turn (recorded durably on the activity): it finished speaking
        // the bridge line, so seat the live call in the queue and offer it rather than hanging up or listening again.
        if (activity.TryGet<PendingVoiceHandoff>(out _))
        {
            await PerformVoiceHandoffAsync(voiceEvent, media, activity, _clock.UtcNow, announced: true, cancellationToken);
            return;
        }

        // The model asked to end the call: it finished speaking its goodbye, so hang up now.
        if (lastAssistant is not null && lastAssistant.Content.Contains(HangupMarker, StringComparison.Ordinal))
        {
            await media.HangupAsync(voiceEvent.ProviderCallId, cancellationToken);
            return;
        }

        // The caller belongs to an agent now, bridged onto this same leg: listening again is how the assistant
        // ended up answering the two of them. Only the after-hours goodbye above still has anything to do.
        if (IsHandedToAgent(activity))
        {
            return;
        }

        // A voicemail whose greeting ended while the assistant was talking is recording now: leave the message.
        if (await LeaveVoicemailOnceTheGreetingHasEndedAsync(voiceEvent, media, activity, cancellationToken))
        {
            return;
        }

        // The agent finished speaking; listen for the caller's reply.
        var listenLanguage = await GetListenLanguageAsync(media, activity, cancellationToken);
        await media.StartTranscriptionAsync(voiceEvent.ProviderCallId, language: listenLanguage, commandId: $"ai-tx-{prompts.Count}", cancellationToken);
        await WatchForSilenceAsync(voiceEvent, prompts.Count, ListeningWait(activity));
    }

    private async Task OnTranscriptionAsync(VoiceAgentEvent voiceEvent, IVoiceAgentMediaProvider media, CancellationToken cancellationToken)
    {
        if (!voiceEvent.TranscriptionIsFinal || string.IsNullOrWhiteSpace(voiceEvent.TranscriptionText))
        {
            return;
        }

        var activity = await _activityStore.FindByIdAsync(voiceEvent.ActivityId, cancellationToken);

        if (activity is null || await IgnoreWhatIsHeardAfterHandoffAsync(voiceEvent, media, activity, cancellationToken))
        {
            return;
        }

        var (profile, session) = await ResolveConversationAsync(activity, cancellationToken);

        if (profile is null || session is null)
        {
            return;
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
            return;
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
            return;
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
            return;
        }

        if (string.IsNullOrWhiteSpace(reply) && !handoffRequested)
        {
            // Nothing to say; keep listening so the call is not stranded silent.
            var listenLanguage = await GetListenLanguageAsync(media, activity, cancellationToken);
            await media.StartTranscriptionAsync(voiceEvent.ProviderCallId, language: listenLanguage, commandId: $"ai-tx-retry-{prompts.Count}", cancellationToken);

            // The caller's turn has been stored since the prompts above were read, so it counts as said.
            await WatchForSilenceAsync(voiceEvent, prompts.Count + 1);
            return;
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
    }

    private async Task OnHangupAsync(VoiceAgentEvent voiceEvent, CancellationToken cancellationToken)
    {
        ReleaseVoicemailClaim(voiceEvent.ActivityId);
        _answeringMachineSignals.Forget(voiceEvent.ProviderName, voiceEvent.ProviderCallId);
        _silenceWatchdog.Forget(voiceEvent.ActivityId);
        _callerKeys.Forget(voiceEvent.ActivityId);

        var activity = await _activityStore.FindByIdAsync(voiceEvent.ActivityId, cancellationToken);

        await RecordTurnBasedSessionAsync(voiceEvent, activity, outcome: null, _clock.UtcNow);

        // This fires when the model's own leg ends, which is also what a handoff looks like from here: the model
        // disconnects the moment the caller is passed to a live agent.
        if (!VoiceCallConclusionPolicy.ShouldConclude(activity))
        {
            if (activity is not null && activity.AiEscalated)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "AI voice activity '{ActivityId}' was handed to a live agent, so its outcome is left to that agent rather than concluded here.",
                        activity.ItemId.SanitizeLogValue());
                }

                // The caller was handed to a queue and has now hung up, which the queue would otherwise never
                // learn: this leg's events are deliberately kept out of Contact Center routing, and that is right
                // until the handover and wrong after it. Left unsaid, the item stays reserved for somebody who is
                // no longer on the line, the hold music plays to a dead leg, and the call is missing from the
                // abandonment figure that exists to show exactly this.
                await ReleaseQueuedCallerAsync(activity.ItemId, cancellationToken);
            }

            await ObserveAsync(voiceEvent, AutomatedVoiceCallObservationKind.ConversationEnded, activity?.AiEscalated == true ? HandedToAgentOutcome : NotConcludedOutcome, cancellationToken: cancellationToken);

            return;
        }

        // Conclusion analysis (summary + disposition) runs in a deferred task so the webhook returns promptly.
        var activityId = activity.ItemId;
        var endedUtc = _clock.UtcNow;

        // A number the network says is not in service was never answered: completed as such, without the review.
        if (TryConcludeNotInService(voiceEvent, activity, endedUtc))
        {
            return;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            try
            {
                await ConcludeAsync(scope.ServiceProvider, activityId, lineBusy: voiceEvent.HangupCause == Telephony.Models.HangupCause.Busy);
                await ObserveConclusionAsync(scope.ServiceProvider, voiceEvent, endedUtc);
            }
            catch (Exception ex)
            {
                scope.ServiceProvider.GetRequiredService<ILogger<VoiceAgentConversationLoop>>()
                    .LogError(ex, "Failed to conclude AI voice activity '{ActivityId}'.", activityId.SanitizeLogValue());
            }
        });
    }

    private async Task<(AIProfile Profile, AIChatSession Session)> ResolveConversationAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        var profileId = activity.AIProfileId;

        if (string.IsNullOrWhiteSpace(profileId))
        {
            var flow = string.IsNullOrWhiteSpace(activity.SubjectContentType)
                ? null
                : await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(activity.SubjectContentType, cancellationToken);
            profileId = flow?.ProfileId;
        }

        var profile = string.IsNullOrWhiteSpace(profileId) ? null : await _profileManager.FindByIdAsync(profileId, cancellationToken);

        if (profile is null || profile.Type != AIProfileType.Chat)
        {
            return (null, null);
        }

        AIChatSession session = null;

        if (!string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            session = await _chatSessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);
        }

        session ??= new AIChatSession
        {
            SessionId = UniqueId.GenerateId(),
            ProfileId = profile.ItemId,
            CreatedUtc = _clock.UtcNow,
            LastActivityUtc = _clock.UtcNow,
            Title = "Automated AI Voice Call",
        };

        if (string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            await _chatSessionManager.SaveAsync(session, cancellationToken);
            activity.AISessionId = session.SessionId;
            await _activityStore.UpdateAsync(activity, cancellationToken);
        }

        return (profile, session);
    }
}
