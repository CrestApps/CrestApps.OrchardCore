using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telnyx.Services;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Json;
using OrchardCore.Liquid;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Drives an automated AI voice conversation over a Telnyx call. The turn loop is a state machine driven purely
/// by Telnyx webhooks and the stored chat transcript, so no per-call server state is required:
/// answered -> speak the greeting; speak.ended -> listen (start transcription); a final transcript -> stop
/// listening, run the LLM, speak the reply; hangup -> summarize, disposition, and run the subject actions.
/// </summary>
public sealed class TelnyxAiVoiceConversationHandler : ITelnyxAiVoiceEventHandler
{
    // Marker the model appends to its final line when it wants to end the call. It is spoken-stripped, but kept
    // in the stored transcript so the speak.ended handler can hang up gracefully after the goodbye finishes.
    private const string HangupMarker = "[[HANGUP]]";

    private readonly IOmnichannelActivityStore _activityStore;
    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAICompletionService _completionService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAICompletionContextBuilder _contextBuilder;
    private readonly IAIProfileManager _profileManager;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly ITelnyxVoiceAgentClient _voiceClient;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IContentManager _contentManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public TelnyxAiVoiceConversationHandler(
        IOmnichannelActivityStore activityStore,
        IAIChatSessionManager chatSessionManager,
        IAIChatSessionPromptStore promptStore,
        IAICompletionService completionService,
        IAIDeploymentManager deploymentManager,
        IAICompletionContextBuilder contextBuilder,
        IAIProfileManager profileManager,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        ITelnyxVoiceAgentClient voiceClient,
        ILiquidTemplateManager liquidTemplateManager,
        IContentManager contentManager,
        IClock clock,
        ILogger<TelnyxAiVoiceConversationHandler> logger)
    {
        _activityStore = activityStore;
        _chatSessionManager = chatSessionManager;
        _promptStore = promptStore;
        _completionService = completionService;
        _deploymentManager = deploymentManager;
        _contextBuilder = contextBuilder;
        _profileManager = profileManager;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _voiceClient = voiceClient;
        _liquidTemplateManager = liquidTemplateManager;
        _contentManager = contentManager;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task HandleAsync(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(state?.ActivityId) || string.IsNullOrWhiteSpace(callEvent?.CallControlId))
        {
            return;
        }

        var eventType = callEvent.EventType?.Trim().ToLowerInvariant();

        try
        {
            switch (eventType)
            {
                case "call.answered":
                    await OnAnsweredAsync(callEvent, state, cancellationToken);
                    break;
                case "call.speak.ended":
                    await OnSpeakEndedAsync(callEvent, state, cancellationToken);
                    break;
                case "call.transcription":
                    await OnTranscriptionAsync(callEvent, state, cancellationToken);
                    break;
                case "call.hangup":
                    await OnHangupAsync(callEvent, state, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred handling the '{EventType}' event of AI voice activity '{ActivityId}'.", eventType, state.ActivityId.SanitizeLogValue());
        }
    }

    private async Task OnAnsweredAsync(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        var activity = await _activityStore.FindByIdAsync(state.ActivityId, cancellationToken);

        if (activity is null)
        {
            return;
        }

        var (profile, session) = await ResolveConversationAsync(activity, cancellationToken);

        if (profile is null || session is null)
        {
            _logger.LogWarning("AI voice call answered but no profile/session for activity '{ActivityId}'.", state.ActivityId.SanitizeLogValue());
            await _voiceClient.HangupAsync(callEvent.CallControlId, cancellationToken);
            return;
        }

        var existingPrompts = await _promptStore.GetPromptsAsync(session.SessionId);

        // Redelivery guard: the greeting is spoken once.
        if (existingPrompts.Any(p => p.Role == ChatRole.Assistant))
        {
            return;
        }

        var greeting = await RenderInitialPromptAsync(activity, profile, session, cancellationToken);

        if (string.IsNullOrWhiteSpace(greeting))
        {
            greeting = "Hi there, this is Alex calling from Prestige Auto Group. Do you have a quick minute?";
        }

        await StorePromptAsync(session, ChatRole.Assistant, greeting, cancellationToken);
        await SpeakAsync(callEvent.CallControlId, profile, greeting, cancellationToken);

        // The customer answered: advance out of AwaitingCustomerAnswer into the live in-progress state. This both
        // records the correct status and takes the activity out of the automated no-response expiry pass window
        // (which only transitions AwaitingCustomerAnswer rows) so that pass cannot race the hangup conclusion and
        // flip a live, answered call to Failed mid-conversation.
        if (activity.Status != ActivityStatus.InProgress)
        {
            activity.Status = ActivityStatus.InProgress;
            await _activityStore.UpdateAsync(activity, cancellationToken);
        }
    }

    private async Task OnSpeakEndedAsync(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        var activity = await _activityStore.FindByIdAsync(state.ActivityId, cancellationToken);

        if (activity is null || string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            return;
        }

        var prompts = await _promptStore.GetPromptsAsync(activity.AISessionId);
        var lastAssistant = prompts.LastOrDefault(p => p.Role == ChatRole.Assistant);

        // The model asked to end the call: it finished speaking its goodbye, so hang up now.
        if (lastAssistant is not null && lastAssistant.Content.Contains(HangupMarker, StringComparison.Ordinal))
        {
            await _voiceClient.HangupAsync(callEvent.CallControlId, cancellationToken);
            return;
        }

        // The agent finished speaking; listen for the caller's reply.
        await _voiceClient.StartTranscriptionAsync(callEvent.CallControlId, language: "en", commandId: $"ai-tx-{prompts.Count}", cancellationToken);
    }

    private async Task OnTranscriptionAsync(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        if (!callEvent.TranscriptionIsFinal || string.IsNullOrWhiteSpace(callEvent.TranscriptionText))
        {
            return;
        }

        var activity = await _activityStore.FindByIdAsync(state.ActivityId, cancellationToken);

        if (activity is null)
        {
            return;
        }

        var (profile, session) = await ResolveConversationAsync(activity, cancellationToken);

        if (profile is null || session is null)
        {
            return;
        }

        // Stop listening while we think and speak, so the agent's own text-to-speech is never transcribed.
        await _voiceClient.StopTranscriptionAsync(callEvent.CallControlId, cancellationToken);

        var caller = callEvent.TranscriptionText.Trim();

        var prompts = await _promptStore.GetPromptsAsync(session.SessionId);
        var lastUser = prompts.LastOrDefault(p => p.Role == ChatRole.User);

        // Redelivery / duplicate final guard.
        if (lastUser is not null && string.Equals(lastUser.Content?.Trim(), caller, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await StorePromptAsync(session, ChatRole.User, caller, cancellationToken);

        var reply = await CompleteAsync(profile, session, cancellationToken);

        if (string.IsNullOrWhiteSpace(reply))
        {
            // Nothing to say; keep listening so the call is not stranded silent.
            await _voiceClient.StartTranscriptionAsync(callEvent.CallControlId, language: "en", commandId: $"ai-tx-retry-{prompts.Count}", cancellationToken);
            return;
        }

        await StorePromptAsync(session, ChatRole.Assistant, reply, cancellationToken);

        var spoken = reply.Replace(HangupMarker, string.Empty, StringComparison.Ordinal).Trim();
        await SpeakAsync(callEvent.CallControlId, profile, spoken, cancellationToken);
    }

    private async Task OnHangupAsync(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        var activity = await _activityStore.FindByIdAsync(state.ActivityId, cancellationToken);

        if (activity is null || activity.Status == ActivityStatus.Completed)
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
                scope.ServiceProvider.GetRequiredService<ILogger<TelnyxAiVoiceConversationHandler>>()
                    .LogError(ex, "Failed to conclude AI voice activity '{ActivityId}'.", activityId.SanitizeLogValue());
            }
        });
    }

    private async Task ConcludeAsync(IServiceProvider services, string activityId)
    {
        var store = services.GetRequiredService<IOmnichannelActivityStore>();
        var activity = await store.FindByIdAsync(activityId);

        if (activity is null || activity.Status == ActivityStatus.Completed || string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            return;
        }

        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var flowSettingsService = services.GetRequiredService<ISubjectFlowSettingsService>();
        var promptStore = services.GetRequiredService<IAIChatSessionPromptStore>();
        var clientFactory = services.GetRequiredService<IAIClientFactory>();
        var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();
        var contextBuilder = services.GetRequiredService<IAICompletionContextBuilder>();
        var contentManager = services.GetRequiredService<IContentManager>();
        var dispositionCatalog = services.GetRequiredService<ICatalog<OmnichannelDisposition>>();
        var actionCatalog = services.GetRequiredService<ISourceCatalog<SubjectAction>>();
        var executor = services.GetRequiredService<ISubjectActionExecutor>();
        var jsonOptions = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DocumentJsonSerializerOptions>>().Value;
        var clock = services.GetRequiredService<IClock>();
        var session = services.GetRequiredService<ISession>();

        var profile = await profileManager.FindByIdAsync(activity.AIProfileId ?? string.Empty);

        if (profile is null)
        {
            return;
        }

        var flowSettings = string.IsNullOrWhiteSpace(activity.SubjectContentType)
            ? null
            : await flowSettingsService.FindConfiguredFlowSettingsAsync(activity.SubjectContentType);

        // Dispositions the AI may choose from: those wired to the subject's actions, falling back to all
        // configured dispositions so a call is never left without a way to be classified.
        var allActions = await actionCatalog.GetAllAsync();
        var subjectDispositionIds = allActions
            .Where(a => string.Equals(a.SubjectContentType, activity.SubjectContentType, StringComparison.OrdinalIgnoreCase))
            .Select(a => a.DispositionId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        var dispositions = subjectDispositionIds.Count > 0
            ? (await dispositionCatalog.GetAsync(subjectDispositionIds)).ToList()
            : (await dispositionCatalog.GetAllAsync()).ToList();

        var sessionPrompts = await promptStore.GetPromptsAsync(activity.AISessionId);

        var transcriptText = string.Join("\n", sessionPrompts
            .Where(p => !p.IsGeneratedPrompt)
            .Select(p => $"{(p.Role == ChatRole.Assistant ? "Agent" : "Customer")}: {p.Content?.Replace(HangupMarker, string.Empty)}"));

        var systemPrompt = """
            You review a finished outbound sales phone call between an AI agent and a customer, and produce a
            structured result as JSON. Write a concise, factual Summary (2-4 sentences) capturing what the
            customer is looking for (vehicle type, timeline, budget, trade-in, any contact details they gave)
            and the outcome. Choose the single DispositionId from the provided list that best matches the
            outcome. If none clearly fits, choose the closest. Only output the requested fields.
            """;

        var userPrompt = $"""
            Call transcript:
            {transcriptText}

            Subject goal: {flowSettings?.SubjectGoal}

            Available dispositions (choose one DispositionId): {JsonSerializer.Serialize(dispositions.Select(d => new { Id = d.ItemId, d.Name, d.Description }))}
            """;

        var conclusionContext = await contextBuilder.BuildAsync(profile, context =>
        {
            context.SystemMessage = systemPrompt;
            context.DisableTools = true;
        });

        var deployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentPurpose.Chat, deploymentName: conclusionContext.ChatDeploymentName);

        if (deployment is null)
        {
            return;
        }

        var client = await clientFactory.CreateChatClientAsync(deployment, builder => builder.UseDefaultResilience());

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt),
        };

        var response = await client.GetResponseAsync<VoiceConclusionResult>(messages, jsonOptions.SerializerOptions);
        var result = response.Result;

        // Decide the disposition and summary from the (read-only) analysis before touching the activity.
        var dispositionId = result?.DispositionId;

        if (string.IsNullOrWhiteSpace(dispositionId) || !dispositions.Any(d => d.ItemId == dispositionId))
        {
            dispositionId = dispositions.FirstOrDefault()?.ItemId;
        }

        var disposition = dispositions.FirstOrDefault(d => d.ItemId == dispositionId);

        var notes = string.IsNullOrWhiteSpace(result?.Summary)
            ? "Automated AI voice call completed."
            : result.Summary;

        // Resolve the content items the subject actions operate on up front. These are content-manager reads that
        // trigger a YesSql session flush; doing them here — before the activity is mutated — keeps that flush from
        // ever trying to persist a dirty, stale activity (which is what surfaced as a ConcurrencyException when the
        // background expiry pass concurrently transitioned the same AwaitingCustomerAnswer activity to Failed).
        var contact = string.IsNullOrWhiteSpace(activity.ContactContentItemId)
            ? null
            : await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);
        var subject = activity.Subject ?? (string.IsNullOrWhiteSpace(activity.SubjectContentType) ? null : await contentManager.NewAsync(activity.SubjectContentType));

        // Terminal write. Reload the activity fresh (the analysis above ran a slow LLM call, during which the row
        // may have moved on) and apply the conclusion. The answered call was advanced to InProgress, which keeps
        // the automated no-response expiry pass — the only other writer of these rows — out of this window, so
        // this write has no competitor. The explicit flush still surfaces any residual conflict here (logged)
        // rather than letting it blow up at the deferred-scope commit outside this try/catch.
        var concluded = await store.FindByIdAsync(activityId);

        if (concluded is null || concluded.Status == ActivityStatus.Completed)
        {
            return;
        }

        concluded.Status = ActivityStatus.Completed;
        concluded.CompletedUtc = clock.UtcNow;
        concluded.Notes = notes;
        concluded.DispositionId = dispositionId;

        await store.UpdateAsync(concluded);

        try
        {
            // Commit the conclusion durably before running the subject actions, so a failing action handler cannot
            // roll back the disposition, and so any residual concurrency conflict is caught here rather than at the
            // deferred-scope commit outside this method's try/catch.
            await session.SaveChangesAsync();
        }
        catch (ConcurrencyException)
        {
            _logger.LogWarning("Concurrency conflict while concluding AI voice activity '{ActivityId}'; another process updated it.", activityId.SanitizeLogValue());

            return;
        }

        if (disposition is not null)
        {
            await executor.ExecuteAsync(new SubjectActionExecutionContext
            {
                Activity = concluded,
                Contact = contact,
                Subject = subject,
                Disposition = disposition,
            });
        }

        _logger.LogInformation("Concluded AI voice activity '{ActivityId}' with disposition '{Disposition}'.", activityId.SanitizeLogValue(), disposition?.Name.SanitizeLogValue());
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

    private async Task<string> CompleteAsync(AIProfile profile, AIChatSession session, CancellationToken cancellationToken)
    {
        var prompts = await _promptStore.GetPromptsAsync(session.SessionId);

        var transcript = prompts
            .Where(p => !p.IsGeneratedPrompt)
            .Select(p => new ChatMessage(p.Role, (p.Content ?? string.Empty).Replace(HangupMarker, string.Empty)));

        var context = await _contextBuilder.BuildAsync(profile, cancellationToken: cancellationToken);
        context.AdditionalProperties["Session"] = session;

        var deployment = await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentPurpose.Chat, deploymentName: context.ChatDeploymentName, cancellationToken: cancellationToken);

        if (deployment is null)
        {
            return null;
        }

        var completion = await _completionService.CompleteAsync(deployment, transcript, context, cancellationToken);

        return completion?.Messages?.FirstOrDefault()?.Text;
    }

    private Task SpeakAsync(string callControlId, AIProfile profile, string text, CancellationToken cancellationToken)
    {
        // Telnyx built-in text-to-speech. A basic named voice keeps this independent of per-account TTS engines.
        _ = profile;

        return _voiceClient.SpeakAsync(callControlId, text, voice: "female", language: "en-US", commandId: null, cancellationToken);
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

    private sealed class VoiceConclusionResult
    {
        public string Summary { get; set; }

        public string DispositionId { get; set; }
    }
}
