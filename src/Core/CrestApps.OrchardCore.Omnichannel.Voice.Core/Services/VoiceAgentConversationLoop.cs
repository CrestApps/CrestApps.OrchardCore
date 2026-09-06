using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Entities;
using OrchardCore.Flows.Models;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Json;
using OrchardCore.Liquid;
using OrchardCore.Modules;
using YesSql;

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
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAICompletionContextBuilder _contextBuilder;
    private readonly IAIProfileManager _profileManager;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly IEnumerable<IOmnichannelHandoffService> _handoffServices;
    private readonly IVoiceAgentMediaProviderResolver _mediaResolver;
    private readonly IRealtimeVoiceConversationRunner _realtimeRunner;
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
        IAIDeploymentManager deploymentManager,
        IAICompletionContextBuilder contextBuilder,
        IAIProfileManager profileManager,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IEnumerable<IOmnichannelHandoffService> handoffServices,
        IVoiceAgentMediaProviderResolver mediaResolver,
        IRealtimeVoiceConversationRunner realtimeRunner,
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
        _deploymentManager = deploymentManager;
        _contextBuilder = contextBuilder;
        _profileManager = profileManager;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _handoffServices = handoffServices;
        _mediaResolver = mediaResolver;
        _realtimeRunner = realtimeRunner;
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

        // A profile configured with a realtime deployment holds the call as a live speech-to-speech session
        // instead of the transcribe-complete-synthesize loop below. That loop cannot begin a reply until the
        // caller has stopped talking, the transcript has come back, the model has answered and the answer has
        // been synthesized; a realtime session answers while they are still finishing, and hears them if they
        // interrupt. Everything downstream is unchanged, because both write the same transcript.
        if (!string.IsNullOrWhiteSpace(profile.RealtimeDeploymentName))
        {
            // The transfer tool records the model's escalation on this turn rather than performing it, so the
            // flag has to start clean for the session we are about to hold.
            _handoffTurn.Reset();

            if (await _realtimeRunner.RunAsync(new RealtimeVoiceConversationContext
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
            }, cancellationToken))
            {
                // A realtime session holds the call for its whole duration, and the model escalates from inside
                // it by invoking the transfer tool — which only RECORDS the request. The turn-based loop below
                // reads that flag after every completion, but this branch used to return without ever looking at
                // it: the caller heard "I'm connecting you to a specialist" and then stayed with the bot, because
                // nothing enqueued them. Honour it here, on the same enqueue-and-offer path the turn-based loop
                // uses, so a handoff means the same thing on both.
                if (_handoffTurn.HandoffRequested)
                {
                    await PerformVoiceHandoffAsync(voiceEvent, media, activity, cancellationToken);
                }

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

        var (reply, handoffRequested, handoffReason) = await CompleteAsync(profile, session, activity, cancellationToken);

        await stopListening;

        if (string.IsNullOrWhiteSpace(reply) && !handoffRequested)
        {
            // Nothing to say; keep listening so the call is not stranded silent.
            await media.StartTranscriptionAsync(voiceEvent.ProviderCallId, language: "en", commandId: $"ai-tx-retry-{prompts.Count}", cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(reply))
        {
            await StorePromptAsync(session, ChatRole.Assistant, reply, cancellationToken);
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

        if (activity is null || activity.Status.IsTerminal())
        {
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

    private async Task<(string Reply, bool HandoffRequested, string Reason)> CompleteAsync(AIProfile profile, AIChatSession session, OmnichannelActivity activity, CancellationToken cancellationToken)
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

        var context = await _contextBuilder.BuildAsync(profile, cancellationToken: cancellationToken);
        context.AdditionalProperties["Session"] = session;

        // Attach the transfer tool to this completion once the context exists. The automated conversation calls the
        // completion service directly rather than through the tool orchestrator, so the tool must be added here or
        // the model never receives it. See the identical fix in the SMS handler.
        if (handoffService is not null)
        {
            AttachTransferToAgentTool(context);
        }

        var deployment = await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentPurpose.Chat, deploymentName: context.ChatDeploymentName, cancellationToken: cancellationToken);

        if (deployment is null)
        {
            return (null, false, null);
        }

        // The completion auto-invokes the transfer tool when the model decides to escalate; the tool records the
        // decision on the scoped turn, which is reset first and read back once the completion returns.
        _handoffTurn.Reset();

        var completion = await _completionService.CompleteAsync(deployment, transcript, context, cancellationToken);

        var reply = completion?.Messages?.FirstOrDefault()?.Text;
        var handoffRequested = handoffService is not null && _handoffTurn.HandoffRequested;

        return (reply, handoffRequested, _handoffTurn.Reason);
    }

    // Attaches the transfer-to-agent tool to this single completion. The automated conversation calls the completion
    // service directly rather than through the tool orchestrator, so the scoped-tool key the function-invocation
    // service handler reads is otherwise never populated and no tools reach the model. We register the transfer tool
    // as a scoped system-tool entry for this turn only (the context is built per turn and never persisted). Enabling
    // it through the profile's tool-name list does not work: the profile tool provider reads the names snapshotted
    // when the context was built and, either way, skips system tools — which the transfer tool is.
    private static void AttachTransferToAgentTool(AICompletionContext context)
    {
        var entry = new ToolRegistryEntry
        {
            Id = OmnichannelHandoffHelper.TransferToAgentToolName,
            Name = OmnichannelHandoffHelper.TransferToAgentToolName,
            Description = "Transfers the current conversation to a live human agent.",
            Source = ToolRegistryEntrySource.System,
            CreateAsync = serviceProvider => ValueTask.FromResult(
                serviceProvider.GetKeyedService<AITool>(OmnichannelHandoffHelper.TransferToAgentToolName)),
        };

        context.AdditionalProperties[FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey] =
            new List<ToolRegistryEntry> { entry };
    }

    // Resolves the phone handoff service and this subject's flow settings, returning a non-null service only when
    // handoff is both configured (enabled with a target queue) and a channel implementation is registered. The flow
    // settings are always returned so callers can read the target queue.
    private async Task<(IOmnichannelHandoffService Service, SubjectFlowSettings FlowSettings)> ResolveVoiceHandoffAsync(
        OmnichannelActivity activity,
        CancellationToken cancellationToken)
    {
        var flowSettings = string.IsNullOrWhiteSpace(activity.SubjectContentType)
            ? null
            : await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(activity.SubjectContentType, cancellationToken);

        if (!OmnichannelHandoffHelper.IsHandoffEnabled(flowSettings))
        {
            return (null, flowSettings);
        }

        var service = _handoffServices?.FirstOrDefault(candidate => candidate.CanHandle(OmnichannelConstants.Channels.Phone));

        return (service, flowSettings);
    }

    // Seats the still-connected caller in the configured queue and offers the call to an agent, reusing the inbound
    // enqueue-and-offer pipeline. On success the call stays up while the queue rings an agent; on failure there is
    // nowhere to route the caller, so the call is ended.
    private async Task PerformVoiceHandoffAsync(VoiceAgentEvent voiceEvent, IVoiceAgentMediaProvider media, OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        var (handoffService, flowSettings) = await ResolveVoiceHandoffAsync(activity, cancellationToken);

        if (handoffService is null)
        {
            _logger.LogWarning("An AI voice handoff was requested for Activity {ActivityId} but no handoff destination is available; ending the call.", activity.ItemId.SanitizeLogValue());
            await media.HangupAsync(voiceEvent.ProviderCallId, cancellationToken);

            return;
        }

        OmnichannelHandoffResult result;

        try
        {
            result = await handoffService.RequestHandoffAsync(new OmnichannelHandoffRequest
            {
                Activity = activity,
                TargetQueueId = flowSettings.HandoffQueueId,
                Reason = "The automated assistant escalated the call to a live agent.",
                ContactAddress = activity.PreferredDestination,
                ProviderName = voiceEvent.ProviderName,
                ProviderCallId = voiceEvent.ProviderCallId,
                // Carried onto the interaction so the answering agent sees what the caller has already been
                // through, and can open the transcript rather than asking them to repeat it.
                AiSessionId = activity.AISessionId,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The AI voice handoff for Activity {ActivityId} threw; ending the call.", activity.ItemId.SanitizeLogValue());
            await media.HangupAsync(voiceEvent.ProviderCallId, cancellationToken);

            return;
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("The AI voice handoff for Activity {ActivityId} did not complete: {Reason}. Ending the call.", activity.ItemId.SanitizeLogValue(), result.Message);
            await media.HangupAsync(voiceEvent.ProviderCallId, cancellationToken);

            return;
        }

        // After hours the destination queue is closed, so a callback was scheduled instead of routing the live
        // call. Tell the caller and end the call gracefully. The closing line is spoken and stored with the hangup
        // marker; the durable handoff flag is cleared first so the next speak.ended reaches the hangup path
        // instead of re-entering the handoff.
        if (result.Disposition == HandoffDisposition.CallbackScheduled)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("AI voice Activity {ActivityId} could not route live (after hours); a callback was scheduled.", activity.ItemId.SanitizeLogValue());
            }

            var concluded = await _activityStore.FindByIdAsync(activity.ItemId, cancellationToken);

            if (concluded is not null && concluded.Properties.Remove(nameof(PendingVoiceHandoff)))
            {
                await _activityStore.UpdateAsync(concluded, cancellationToken);
            }

            const string closing = "Thanks for your patience. Our specialists aren't available right now, so we've scheduled a callback and someone will reach out to you shortly. Goodbye.";

            if (!string.IsNullOrWhiteSpace(activity.AISessionId))
            {
                var session = await _chatSessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);

                if (session is not null)
                {
                    // Store with the hangup marker so the next speak.ended ends the call.
                    await StorePromptAsync(session, ChatRole.Assistant, closing + " " + HangupMarker, cancellationToken);
                }
            }

            await SpeakAsync(media, voiceEvent.ProviderCallId, activity, closing, cancellationToken);

            return;
        }

        // "Connecting you now" and "you are in a queue" are different promises. Saying the first to a caller
        // nobody is free to take leaves them listening to silence, waiting for a person who was never offered
        // the call, so each disposition gets its own line.
        var routed = result.Disposition == HandoffDisposition.Routed;

        var handoffLine = routed
            ? "Thanks for waiting. I'm connecting you to a specialist now."
            : "Thanks for waiting. All of our specialists are busy right now, so I've placed you in the queue and the next available person will be with you shortly.";

        await SpeakAsync(media, voiceEvent.ProviderCallId, activity, handoffLine, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Handed off AI voice Activity {ActivityId} to a live agent; {OfferState}.",
                activity.ItemId.SanitizeLogValue(),
                routed ? "the call was offered to an available agent" : "the call is held while the queue waits for one");
        }
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
