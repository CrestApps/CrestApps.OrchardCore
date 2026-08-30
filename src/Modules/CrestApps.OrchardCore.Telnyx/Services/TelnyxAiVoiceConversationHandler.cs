using System.Text.Json;
using System.Text.Json.Nodes;
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

    // Default text-to-speech voice used when the activity has no configured voice. A neural voice (rather than the
    // basic "female"/"male" Telnyx voices) is the single biggest lever against robotic-sounding delivery. This must
    // be a voice the Telnyx account supports for the speak command; it is overridden per-activity by the configured
    // TextToSpeechVoiceId chosen at inventory load / on the subject flow.
    private const string DefaultVoice = "AWS.Polly.Joanna-Neural";

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
        await SpeakAsync(callEvent.CallControlId, activity, greeting, cancellationToken);

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

        // Stop listening while we think and speak, so the agent's own text-to-speech is never transcribed. Start
        // the stop now but let the LLM turn run concurrently, so its Telnyx round-trip overlaps the model latency
        // rather than adding to it. The stop is awaited before we speak, so the ordering guarantee is preserved.
        var stopListening = _voiceClient.StopTranscriptionAsync(callEvent.CallControlId, cancellationToken);

        var caller = callEvent.TranscriptionText.Trim();

        var prompts = await _promptStore.GetPromptsAsync(session.SessionId);
        var lastUser = prompts.LastOrDefault(p => p.Role == ChatRole.User);

        // Redelivery / duplicate final guard.
        if (lastUser is not null && string.Equals(lastUser.Content?.Trim(), caller, StringComparison.OrdinalIgnoreCase))
        {
            await stopListening;
            return;
        }

        await StorePromptAsync(session, ChatRole.User, caller, cancellationToken);

        var reply = await CompleteAsync(profile, session, cancellationToken);

        await stopListening;

        if (string.IsNullOrWhiteSpace(reply))
        {
            // Nothing to say; keep listening so the call is not stranded silent.
            await _voiceClient.StartTranscriptionAsync(callEvent.CallControlId, language: "en", commandId: $"ai-tx-retry-{prompts.Count}", cancellationToken);
            return;
        }

        await StorePromptAsync(session, ChatRole.Assistant, reply, cancellationToken);

        var spoken = reply.Replace(HangupMarker, string.Empty, StringComparison.Ordinal).Trim();
        await SpeakAsync(callEvent.CallControlId, activity, spoken, cancellationToken);
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

        // The AI field-update guards are a snapshot taken when the automated inventory was loaded (the subject
        // AI-settings UI is inbound-only, so an outbound automated inventory configures these on the batch). Only
        // when a guard is on do we both show the model the current content item and apply what it returns.
        var allowUpdateSubject = activity.AllowAIToUpdateSubject;
        var allowUpdateContact = activity.AllowAIToUpdateContact;

        // Resolve the content items the analysis and subject actions operate on up front. These are content-manager
        // reads that trigger a YesSql session flush; doing them here — before the activity is mutated — keeps that
        // flush from ever trying to persist a dirty, stale activity (which surfaced as a ConcurrencyException when
        // the background expiry pass concurrently transitioned the same AwaitingCustomerAnswer activity to Failed).
        var contact = string.IsNullOrWhiteSpace(activity.ContactContentItemId)
            ? null
            : await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);
        var subject = activity.Subject ?? (string.IsNullOrWhiteSpace(activity.SubjectContentType) ? null : await contentManager.NewAsync(activity.SubjectContentType));

        // The subject's updatable text fields, read from the content type definition so the model is asked for the
        // exact fields that exist (rather than authoring a free-form content item, which produced values in shapes
        // the field editors could not read).
        var definitionManager = services.GetRequiredService<IContentDefinitionManager>();
        var subjectTextFields = allowUpdateSubject && !string.IsNullOrWhiteSpace(activity.SubjectContentType)
            ? GetSubjectTextFields(await definitionManager.GetTypeDefinitionAsync(activity.SubjectContentType))
            : [];

        var systemPrompt = $$"""
            You review a finished outbound sales phone call between an AI agent and a customer, and produce a
            structured result as JSON. Always write a concise, factual Summary (2-4 sentences) capturing what the
            customer is looking for (vehicle type, timeline, budget, trade-in, any contact details they gave)
            and the outcome. Always choose the single DispositionId from the provided list that best matches the
            outcome; if none clearly fits, choose the closest.
            {{((allowUpdateSubject && subjectTextFields.Count > 0) ? "You are given a list of subject fields. Return SubjectFields as a JSON object mapping the exact field key shown to a short plain-text value, for any field the call clearly revealed; omit fields you did not learn and never invent keys." : "Do not return SubjectFields.")}}
            {{(allowUpdateContact ? "If, and only if, the customer clearly stated an email address to use for follow-up, set ContactEmail to that exact address (lowercased, with no surrounding words); if it matches the current email on file or none was given, omit ContactEmail." : "Do not return ContactEmail.")}}
            Only output the requested fields.
            """;

        var userPrompt = $"""
            Call transcript:
            {transcriptText}

            Subject goal: {flowSettings?.SubjectGoal}

            Available dispositions (choose one DispositionId): {JsonSerializer.Serialize(dispositions.Select(d => new { Id = d.ItemId, d.Name, d.Description }))}
            """;

        if (allowUpdateSubject && subject is not null && subjectTextFields.Count > 0)
        {
            var subjectContent = (JsonObject)subject.Content;
            var fieldList = subjectTextFields.Select(f =>
            {
                var key = $"{f.Part}.{f.Field}";
                var current = (subjectContent[f.Part]?[f.Field]?["Text"])?.ToString();
                return string.IsNullOrWhiteSpace(current) ? key : $"{key} (current: {current})";
            });

            userPrompt += $"{Environment.NewLine}{Environment.NewLine}Subject fields you may set (return these keys in SubjectFields): {string.Join("; ", fieldList)}";
        }

        if (allowUpdateContact && contact is not null)
        {
            userPrompt += $"{Environment.NewLine}{Environment.NewLine}Current contact email on file: {GetContactEmail(contact) ?? "(none)"}";
        }

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

        // Notes and disposition are written together in this single terminal update, so a concluded call is never
        // dispositioned without notes: the notes fall back to a default line when the model returns no summary.
        concluded.Notes = notes;
        concluded.DispositionId = dispositionId;

        // Gated subject write-back: only when the inventory-load guard allowed it and the model returned values for
        // known fields. Each value is written into the field's real structure (a TextField's Text property) rather
        // than merging a model-authored content item, which produced shapes the field editors could not read. The
        // subject lives on the activity, so it must be applied before the activity is persisted below.
        if (allowUpdateSubject && subject is not null && ApplySubjectFields(subject, result?.SubjectFields, subjectTextFields))
        {
            concluded.Subject = subject;
        }

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

        // Gated contact write-back: the contact is a separate content item, so it is updated after the conclusion
        // is durably committed above — a failing contact save cannot then roll back the disposition. Rather than
        // deep-merging a model-authored content item (which appends duplicate contact-method items and cannot
        // build a correctly structured EmailAddress), we upsert only a captured email into the ContactMethods bag,
        // mirroring how the contact importer constructs those items.
        if (allowUpdateContact && contact is not null && TryApplyContactEmail(contact, result?.ContactEmail))
        {
            await contentManager.UpdateAsync(contact);

            _logger.LogInformation("AI voice activity '{ActivityId}' saved a customer-provided email to the contact.", activityId.SanitizeLogValue());
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

    private Task SpeakAsync(string callControlId, OmnichannelActivity activity, string text, CancellationToken cancellationToken)
    {
        // Honor the configured neural text-to-speech voice for a natural (non-robotic) delivery, falling back to a
        // Telnyx-native neural voice. The value must be a voice the Telnyx account supports for the speak command.
        var voice = string.IsNullOrWhiteSpace(activity?.TextToSpeechVoiceId)
            ? DefaultVoice
            : activity.TextToSpeechVoiceId.Trim();

        return _voiceClient.SpeakAsync(callControlId, text, voice: voice, language: "en-US", commandId: null, cancellationToken);
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

    /// <summary>
    /// Reads the current email address stored on the contact's ContactMethods bag, if any.
    /// </summary>
    internal static string GetContactEmail(ContentItem contact)
    {
        if (!contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bag) || bag.ContentItems is null)
        {
            return null;
        }

        foreach (var method in bag.ContentItems)
        {
            if (string.Equals(method.ContentType, OmnichannelConstants.ContentTypes.EmailAddress, StringComparison.Ordinal) &&
                method.TryGet<EmailInfoPart>(out var emailPart) &&
                !string.IsNullOrWhiteSpace(emailPart.Email?.Text))
            {
                return emailPart.Email.Text.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Upserts a customer-provided email into the contact's ContactMethods bag as a properly structured
    /// EmailAddress content item, replacing any existing one. Returns whether the contact was changed. This
    /// mirrors the construction the contact importer uses, so downstream indexing and exports read it the same
    /// way, and it never appends duplicates the way a raw content-item merge did.
    /// </summary>
    internal static bool TryApplyContactEmail(ContentItem contact, string email)
    {
        if (contact is null || string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        email = email.Trim();

        // A conservative sanity check so a mis-transcribed phrase is never written as an email.
        if (email.Length < 5 || !email.Contains('@', StringComparison.Ordinal) || email.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        // Nothing to do when the same address is already on file.
        if (string.Equals(GetContactEmail(contact), email, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var bag = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods);
        bag.ContentItems ??= [];
        bag.ContentItems.RemoveAll(method => string.Equals(method.ContentType, OmnichannelConstants.ContentTypes.EmailAddress, StringComparison.Ordinal));

        var emailItem = new ContentItem
        {
            ContentType = OmnichannelConstants.ContentTypes.EmailAddress,
            DisplayText = email,
        };

        emailItem.Alter<EmailInfoPart>(part => part.Email = new TextField { Text = email });
        bag.ContentItems.Add(emailItem);
        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, bag);

        return true;
    }

    /// <summary>
    /// Lists the TextField fields declared on the subject content type, as (part, field) pairs. These are the only
    /// fields the conclusion is allowed to set, so the model is never asked to author arbitrary structure.
    /// </summary>
    internal static List<(string Part, string Field)> GetSubjectTextFields(ContentTypeDefinition typeDefinition)
    {
        var fields = new List<(string, string)>();

        if (typeDefinition is null)
        {
            return fields;
        }

        foreach (var part in typeDefinition.Parts)
        {
            foreach (var field in part.PartDefinition.Fields)
            {
                if (string.Equals(field.FieldDefinition?.Name, nameof(TextField), StringComparison.Ordinal))
                {
                    fields.Add((part.Name, field.Name));
                }
            }
        }

        return fields;
    }

    /// <summary>
    /// Writes the model-provided values into the subject's TextField fields using their real <c>Text</c> structure.
    /// Only keys that match a known field (by "Part.Field" or bare field name) and carry a non-empty value are
    /// applied. Returns whether the subject changed.
    /// </summary>
    internal static bool ApplySubjectFields(ContentItem subject, IDictionary<string, string> values, List<(string Part, string Field)> fields)
    {
        if (subject is null || values is null || values.Count == 0 || fields.Count == 0)
        {
            return false;
        }

        var changed = false;

        // ContentItem.Content is a dynamic JsonDynamicObject; cast to the underlying JsonObject so type checks and
        // writes operate on the real node. Going through the dynamic on every access hands back a wrapper that is
        // never a JsonObject, which made each field create a fresh part object that clobbered the previous ones.
        var content = (JsonObject)subject.Content;

        foreach (var (part, field) in fields)
        {
            if (!(values.TryGetValue($"{part}.{field}", out var value) || values.TryGetValue(field, out value)) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (content[part] is not JsonObject partObject)
            {
                partObject = new JsonObject();
                content[part] = partObject;
            }

            partObject[field] = new JsonObject { ["Text"] = value.Trim() };
            changed = true;
        }

        return changed;
    }

    private sealed class VoiceConclusionResult
    {
        public string Summary { get; set; }

        public string DispositionId { get; set; }

        /// <summary>
        /// Gets or sets the subject field values the AI captured, keyed by the "Part.Field" path shown to it, when
        /// allowed. Null otherwise.
        /// </summary>
        public Dictionary<string, string> SubjectFields { get; set; }

        /// <summary>
        /// Gets or sets the email address the customer provided for follow-up, when allowed. Null otherwise.
        /// </summary>
        public string ContactEmail { get; set; }
    }
}
