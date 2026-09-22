using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
using CrestApps.OrchardCore.Telephony.Services;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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
    private const string HangupMarker = "[[HANGUP]]";


    private readonly IOmnichannelActivityStore _activityStore;
    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAICompletionService _completionService;
    private readonly IOmnichannelHandoffTurn _handoffTurn;
    private readonly IVoiceCallEndTurn _endCallTurn;
    private readonly IRealtimeCallCompletionRunner _completionRunner;

    // Optional: automated voice runs on tenants with no Contact Center, which have no queue to release.
    private readonly IEnumerable<IQueuedCallerAbandonmentHandler> _abandonmentHandlers;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIDeploymentCapabilityService _capabilityService;
    private readonly IAICompletionContextBuilder _contextBuilder;
    private readonly IAIProfileManager _profileManager;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly IEnumerable<IOmnichannelHandoffService> _handoffServices;
    private readonly IVoiceAgentMediaProviderResolver _mediaResolver;
    private readonly IRealtimeVoiceConversationRunner _realtimeRunner;
    private readonly ITurnBasedSilenceWatchdog _silenceWatchdog;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IContentManager _contentManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public VoiceAgentConversationLoop(
        IOmnichannelActivityStore activityStore,
        IAIChatSessionManager chatSessionManager,
        IAIChatSessionPromptStore promptStore,
        IAICompletionService completionService,
        IOmnichannelHandoffTurn handoffTurn,
        IVoiceCallEndTurn endCallTurn,
        IRealtimeCallCompletionRunner completionRunner,
        IEnumerable<IQueuedCallerAbandonmentHandler> abandonmentHandlers,
        IAIDeploymentManager deploymentManager,
        IAIDeploymentCapabilityService capabilityService,
        IAICompletionContextBuilder contextBuilder,
        IAIProfileManager profileManager,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IEnumerable<IOmnichannelHandoffService> handoffServices,
        IVoiceAgentMediaProviderResolver mediaResolver,
        IRealtimeVoiceConversationRunner realtimeRunner,
        ITurnBasedSilenceWatchdog silenceWatchdog,
        ILiquidTemplateManager liquidTemplateManager,
        IContentManager contentManager,
        IClock clock,
        ILogger<VoiceAgentConversationLoop> logger)
    {
        _activityStore = activityStore;
        _chatSessionManager = chatSessionManager;
        _promptStore = promptStore;
        _completionService = completionService;
        _handoffTurn = handoffTurn;
        _endCallTurn = endCallTurn;
        _completionRunner = completionRunner;
        _abandonmentHandlers = abandonmentHandlers;
        _deploymentManager = deploymentManager;
        _capabilityService = capabilityService;
        _contextBuilder = contextBuilder;
        _profileManager = profileManager;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _handoffServices = handoffServices;
        _mediaResolver = mediaResolver;
        _realtimeRunner = realtimeRunner;
        _silenceWatchdog = silenceWatchdog;
        _liquidTemplateManager = liquidTemplateManager;
        _contentManager = contentManager;
        _clock = clock;
        _logger = logger;
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
            switch (voiceEvent.Kind)
            {
                case VoiceAgentEventKind.Answered:
                    await OnAnsweredAsync(voiceEvent, media, cancellationToken);
                    break;
                case VoiceAgentEventKind.SpeechEnded:
                    await OnSpeakEndedAsync(voiceEvent, media, cancellationToken);
                    break;
                case VoiceAgentEventKind.Transcription:
                    await OnTranscriptionAsync(voiceEvent, media, cancellationToken);
                    break;
                case VoiceAgentEventKind.Hangup:
                    await OnHangupAsync(voiceEvent, cancellationToken);
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
        await MarkInProgressAsync(activity, cancellationToken);

        // A profile whose model can hold a live conversation takes the call as a speech-to-speech session instead
        // of the transcribe-complete-synthesize loop below. That loop cannot begin a reply until the caller has
        // stopped talking, the transcript has come back, the model has answered and the answer has been
        // synthesized; a realtime session answers while they are still finishing, and hears them if they
        // interrupt. Everything downstream is unchanged, because both write the same transcript.
        //
        // Realtime is a capability of the chat deployment now rather than a deployment of its own, so the
        // question is no longer "was a second deployment configured" but "can the one this profile already uses
        // do it".
        var realtimeDeploymentName = await ResolveRealtimeDeploymentNameAsync(profile, cancellationToken);

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

            var sessionHeldTheCall = false;

            try
            {
                sessionHeldTheCall = await _realtimeRunner.RunAsync(new RealtimeVoiceConversationContext
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

                    // Only when this call actually has an agent to reach. Telling a model it may transfer, on a
                    // call where nothing can receive the caller, promises the caller a person who is not coming.
                    HandoffInstructions = realtimeHandoffService is null
                        ? null
                        : OmnichannelHandoffHelper.BuildHandoffInstructions(realtimeFlowSettings),

                    // The deployment the live session is held on, resolved above by capability.
                    RealtimeDeploymentName = realtimeDeploymentName,

                    // So the assistant addresses the person it actually called, rather than a name it invented.
                    ContactName = await ResolveContactNameAsync(activity, cancellationToken),
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
                await FinishTheCallElsewhereAsync(activity, voiceEvent);
            }

            if (sessionHeldTheCall)
            {
                return;
            }
        }

        var greeting = await RenderInitialPromptAsync(activity, profile, session, cancellationToken);

        if (string.IsNullOrWhiteSpace(greeting))
        {
            greeting = "Hi there, this is Alex calling from Prestige Auto Group. Do you have a quick minute?";
        }

        await StorePromptAsync(session, ChatRole.Assistant, greeting, cancellationToken);
        await SpeakAsync(media, voiceEvent.ProviderCallId, activity, greeting, cancellationToken);
    }

    /// <summary>
    /// Moves an answered call into the live in-progress state.
    /// </summary>
    /// <remarks>
    /// This both records the correct status and takes the activity out of the automated no-response expiry pass
    /// window — that pass only transitions rows still awaiting an answer — so it cannot race the conclusion and
    /// flip a live, answered call to Failed while somebody is still on it.
    /// </remarks>
    /// <summary>
    /// The deployment a live session would be held on, or <see langword="null"/> when this profile cannot hold one.
    /// </summary>
    /// <remarks>
    /// Realtime used to be its own deployment on the profile; it is a model capability now, so the profile's chat
    /// deployment is asked whether it declares it. A profile that names no chat deployment falls back to whatever
    /// deployment the tenant has with the capability, which is how the rest of the platform resolves it.
    /// </remarks>
    /// <param name="profile">The profile driving the conversation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <summary>
    /// The name of the person being called, or <see langword="null"/> when the contact has none.
    /// </summary>
    /// <param name="activity">The call's activity.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <summary>
    /// Tells the Contact Center that a caller waiting for an agent has gone.
    /// </summary>
    /// <remarks>
    /// Nothing thrown here is allowed out. This runs while a call is ending, and a queue that cannot be reached
    /// must not stop the rest of the hangup from being handled.
    /// </remarks>
    /// <param name="activityItemId">The activity the caller was handed over on.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private async Task ReleaseQueuedCallerAsync(string activityItemId, CancellationToken cancellationToken)
    {
        foreach (var handler in _abandonmentHandlers)
        {
            try
            {
                await handler.CallerAbandonedAsync(activityItemId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not release the queue work for the caller who left activity '{ActivityId}'.",
                    activityItemId.SanitizeLogValue());
            }
        }
    }

    private async Task<string> ResolveContactNameAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activity?.ContactContentItemId))
        {
            return null;
        }

        var contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

        return string.IsNullOrWhiteSpace(contact?.DisplayText) ? null : contact.DisplayText.Trim();
    }

    private async Task<string> ResolveRealtimeDeploymentNameAsync(AIProfile profile, CancellationToken cancellationToken)
    {
        // Asked of the deployment catalog by capability, not through the chat slot. A speech-to-speech model
        // cannot serve a text completion, so the framework excludes the realtime feature from that slot -- which
        // means asking the chat slot to resolve the profile's deployment answers "no such deployment" for
        // precisely the deployment being looked for. Nothing failed when it did: the call connected, the
        // assistant spoke, and the only symptom was that it could not hear the caller while it was talking.
        //
        // With no deployment named, this falls back to whatever deployment the tenant has with the capability,
        // which is how the rest of the platform resolves it.
        var deployment = await _capabilityService.ResolveDeploymentWithFeatureAsync(
            AIDeploymentFeatureNames.Realtime,
            profile.ChatDeploymentName,
            cancellationToken);

        if (deployment is not null)
        {
            return deployment.Name;
        }

        // Said plainly on the record, because running turn-based is not an error and produces no other trace:
        // the difference is audible on the phone and invisible in the log.
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Profile '{ProfileName}' runs the turn-based voice loop: deployment '{DeploymentName}' does not declare the '{Feature}' capability.",
                profile.Name.SanitizeLogValue(),
                profile.ChatDeploymentName.SanitizeLogValue(),
                AIDeploymentFeatureNames.Realtime);
        }

        return null;
    }

    private async Task MarkInProgressAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        if (activity.Status == ActivityStatus.InProgress)
        {
            return;
        }

        activity.Status = ActivityStatus.InProgress;
        await _activityStore.UpdateAsync(activity, cancellationToken);
    }

    private async Task OnSpeakEndedAsync(VoiceAgentEvent voiceEvent, IVoiceAgentMediaProvider media, CancellationToken cancellationToken)
    {
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
            await PerformVoiceHandoffAsync(voiceEvent, media, activity, cancellationToken);
            return;
        }

        // The model asked to end the call: it finished speaking its goodbye, so hang up now.
        if (lastAssistant is not null && lastAssistant.Content.Contains(HangupMarker, StringComparison.Ordinal))
        {
            await media.HangupAsync(voiceEvent.ProviderCallId, cancellationToken);
            return;
        }

        // The agent finished speaking; listen for the caller's reply.
        await media.StartTranscriptionAsync(voiceEvent.ProviderCallId, language: "en", commandId: $"ai-tx-{prompts.Count}", cancellationToken);
        await WatchForSilenceAsync(voiceEvent, prompts.Count);
    }

    private async Task OnTranscriptionAsync(VoiceAgentEvent voiceEvent, IVoiceAgentMediaProvider media, CancellationToken cancellationToken)
    {
        if (!voiceEvent.TranscriptionIsFinal || string.IsNullOrWhiteSpace(voiceEvent.TranscriptionText))
        {
            return;
        }

        var activity = await _activityStore.FindByIdAsync(voiceEvent.ActivityId, cancellationToken);

        if (activity is null)
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

        await StorePromptAsync(session, ChatRole.User, caller, cancellationToken);

        var (reply, handoffRequested, handoffReason, endCallRequested) = await CompleteAsync(profile, session, activity, cancellationToken);

        await stopListening;

        if (string.IsNullOrWhiteSpace(reply) && !handoffRequested)
        {
            // Nothing to say; keep listening so the call is not stranded silent.
            await media.StartTranscriptionAsync(voiceEvent.ProviderCallId, language: "en", commandId: $"ai-tx-retry-{prompts.Count}", cancellationToken);

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
        var activity = await _activityStore.FindByIdAsync(voiceEvent.ActivityId, cancellationToken);

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

            return;
        }

        // Conclusion analysis (summary + disposition) runs in a deferred task so the webhook returns promptly.
        var activityId = activity.ItemId;

        ShellScope.AddDeferredTask(async scope =>
        {
            try
            {
                await ConcludeAsync(scope.ServiceProvider, activityId);
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

    private async Task<string> RenderInitialPromptAsync(OmnichannelActivity activity, AIProfile profile, AIChatSession session, CancellationToken cancellationToken)
    {
        var metadata = profile.GetOrCreate<AIProfileMetadata>();
        var pattern = metadata.InitialPrompt?.Trim();

        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        var contact = string.IsNullOrWhiteSpace(activity.ContactContentItemId)
            ? null
            : await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

        var templateContext = new Dictionary<string, FluidValue>
        {
            ["Activity"] = new ObjectValue(activity),
            ["Profile"] = new ObjectValue(profile),
            ["Session"] = new ObjectValue(session),
        };

        if (contact is not null)
        {
            templateContext["Contact"] = new ObjectValue(contact);
        }

        var rendered = await _liquidTemplateManager.RenderStringAsync(pattern, NullEncoder.Default, templateContext);

        return rendered?.Trim();
    }

    private async Task<(string Reply, bool HandoffRequested, string Reason, bool EndCallRequested)> CompleteAsync(AIProfile profile, AIChatSession session, OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        var prompts = await _promptStore.GetPromptsAsync(session.SessionId);

        var transcript = prompts
            .Where(p => !p.IsGeneratedPrompt)
            .Select(p => new ChatMessage(p.Role, (p.Content ?? string.Empty).Replace(HangupMarker, string.Empty)))
            .ToList();

        // When a live agent is available, enable the transfer tool for this turn and guide the model on when to
        // escalate. Guidance is injected as a leading system message so the persona system prompt is preserved.
        var (handoffService, flowSettings) = await ResolveVoiceHandoffAsync(activity, cancellationToken);

        if (handoffService is not null)
        {
            var handoffInstructions = OmnichannelHandoffHelper.BuildHandoffInstructions(flowSettings);

            if (!string.IsNullOrEmpty(handoffInstructions))
            {
                transcript.Insert(0, new ChatMessage(ChatRole.System, handoffInstructions));
            }
        }

        // The same thing the live session is told, because a turn-based call is the same call: somebody has to
        // hang up, and the model is the only one here who knows the conversation is over.
        transcript.Insert(0, new ChatMessage(ChatRole.System, VoiceCallGuidance.EndingTheCall));

        var context = await _contextBuilder.BuildAsync(profile, cancellationToken: cancellationToken);
        context.AdditionalProperties["Session"] = session;

        // Attach the transfer tool to this completion once the context exists. The automated conversation calls the
        // completion service directly rather than through the tool orchestrator, so the tool must be added here or
        // the model never receives it. See the identical fix in the SMS handler.
        AttachCallTools(context, offerTransfer: handoffService is not null);

        var deployment = await _deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, deploymentName: context.ChatDeploymentName, cancellationToken: cancellationToken);

        if (deployment is null)
        {
            return (null, false, null, false);
        }

        // The completion auto-invokes these tools when the model decides to escalate or to finish; each records
        // its decision on a scoped turn, which is reset first and read back once the completion returns.
        _handoffTurn.Reset();
        _endCallTurn.Reset();

        var completion = await _completionService.CompleteAsync(deployment, transcript, context, cancellationToken);

        var reply = completion?.Messages?.FirstOrDefault()?.Text;
        var handoffRequested = handoffService is not null && _handoffTurn.HandoffRequested;

        // A call being handed to a person is not a call that is over, whatever the model asked for alongside it.
        var endCallRequested = _endCallTurn.EndCallRequested && !handoffRequested;

        return (reply, handoffRequested, _handoffTurn.Reason, endCallRequested);
    }

    // Attaches the transfer-to-agent tool to this single completion. The automated conversation calls the completion
    // service directly rather than through the tool orchestrator, so the scoped-tool key the function-invocation
    // service handler reads is otherwise never populated and no tools reach the model. We register the transfer tool
    // as a scoped system-tool entry for this turn only (the context is built per turn and never persisted). Enabling
    // it through the profile's tool-name list does not work: the profile tool provider reads the names snapshotted
    // when the context was built and, either way, skips system tools — which the transfer tool is.
    private static void AttachCallTools(AICompletionContext context, bool offerTransfer)
    {
        // The end-call tool is offered on every turn, and the transfer tool only when this call has somewhere to
        // transfer to. Ending the call needs no such condition: the model can always be finished talking, and a
        // call it cannot end is one that ends when the customer works out that nobody is going to hang up.
        var entries = new List<ToolRegistryEntry>
        {
            new()
            {
                Id = EndCallTool.ToolName,
                Name = EndCallTool.ToolName,
                Description = "Ends the phone call once the conversation has genuinely finished.",
                Source = ToolRegistryEntrySource.System,
                CreateAsync = serviceProvider => ValueTask.FromResult(
                    serviceProvider.GetKeyedService<AITool>(EndCallTool.ToolName)),
            },
        };

        if (offerTransfer)
        {
            entries.Add(new ToolRegistryEntry
            {
                Id = OmnichannelHandoffHelper.TransferToAgentToolName,
                Name = OmnichannelHandoffHelper.TransferToAgentToolName,
                Description = "Transfers the current conversation to a live human agent.",
                Source = ToolRegistryEntrySource.System,
                CreateAsync = serviceProvider => ValueTask.FromResult(
                    serviceProvider.GetKeyedService<AITool>(OmnichannelHandoffHelper.TransferToAgentToolName)),
            });
        }

        context.AdditionalProperties[FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey] = entries;
    }

    private static Task<bool> SpeakAsync(IVoiceAgentMediaProvider media, string providerCallId, OmnichannelActivity activity, string text, CancellationToken cancellationToken)
    {
        // Honor the configured neural text-to-speech voice for a natural (non-robotic) delivery, falling back to
        // the provider's own default. The value must be a voice the provider supports for its speak command.
        var voice = string.IsNullOrWhiteSpace(activity?.TextToSpeechVoiceId)
            ? media.DefaultVoice
            : activity.TextToSpeechVoiceId.Trim();

        return media.SpeakAsync(providerCallId, text, voice: voice, language: "en-US", cancellationToken: cancellationToken);
    }

    private async Task StorePromptAsync(AIChatSession session, ChatRole role, string content, CancellationToken cancellationToken)
    {
        await _promptStore.CreateAsync(new AIChatSessionPrompt
        {
            ItemId = UniqueId.GenerateId(),
            SessionId = session.SessionId,
            Role = role,
            Content = content,
        }, cancellationToken);

        session.LastActivityUtc = _clock.UtcNow;
        await _chatSessionManager.SaveAsync(session, cancellationToken);
    }
}
