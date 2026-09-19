using CrestApps.Core.Omnichannel.Services;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.OrchardCore.Omnichannel.Models;
using CrestApps.OrchardCore.Omnichannel.Services;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Json;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The end of an automated voice conversation: once the call is over, review the transcript, write a summary and
/// a disposition, and apply whatever the call revealed to the subject and the contact.
/// </summary>
public sealed partial class VoiceAgentConversationLoop
{
    private async Task ConcludeAsync(IServiceProvider services, string activityId)
    {
        var store = services.GetRequiredService<IOmnichannelActivityStore>();
        var activity = await store.FindByIdAsync(activityId);

        if (activity is null || activity.Status.IsTerminal() || string.IsNullOrWhiteSpace(activity.AISessionId))
        {
            return;
        }

        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var flowSettingsService = services.GetRequiredService<ISubjectFlowSettingsService>();
        var promptStore = services.GetRequiredService<IAIChatSessionPromptStore>();
        var clientFactory = services.GetRequiredService<IAIClientFactory>();
        var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();
        var contextBuilder = services.GetRequiredService<IAICompletionContextBuilder>();
        var contactResolver = services.GetRequiredService<IOmnichannelContactResolver>();
        var contactWriter = services.GetRequiredService<IOmnichannelContactWriter>();
        var subjectWriter = services.GetRequiredService<IActivitySubjectWriter>();
        var dispositionCatalog = services.GetRequiredService<ICatalog<OmnichannelDisposition>>();
        var actionCatalog = services.GetRequiredService<ISourceCatalog<SubjectAction>>();
        var executor = services.GetRequiredService<ISubjectActionExecutor>();
        var jsonOptions = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DocumentJsonSerializerOptions>>().Value;
        var timeProvider = services.GetRequiredService<TimeProvider>();
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
        var subjectDispositionIds = VoiceCallConclusionPolicy.ResolveSubjectDispositionIds(allActions, activity.SubjectContentType);

        var dispositions = subjectDispositionIds.Count > 0
            ? (await dispositionCatalog.GetAsync(subjectDispositionIds)).ToList()
            : (await dispositionCatalog.GetAllAsync()).ToList();

        var sessionPrompts = await promptStore.GetPromptsAsync(activity.AISessionId);

        var spokenTurns = sessionPrompts
            .Where(p => !p.IsGeneratedPrompt && !string.IsNullOrWhiteSpace(p.Content))
            .ToList();

        var transcriptText = string.Join("\n", spokenTurns
            .Select(p => $"{(p.Role == ChatRole.Assistant ? "Agent" : "Customer")}: {p.Content?.Replace(HangupMarker, string.Empty)}"));

        // Whether anybody actually said anything. A call that rang out, was declined, or was answered and hung up
        // on leaves no turns at all, and there is nothing for the review below to read.
        var hasConversation = VoiceCallConclusionPolicy.HasConversation(sessionPrompts);

        // The AI field-update guards are a snapshot taken when the automated inventory was loaded (the subject
        // AI-settings UI is inbound-only, so an outbound automated inventory configures these on the batch). Only
        // when a guard is on do we both show the model the current content item and apply what it returns.
        var allowUpdateSubject = activity.AllowAIToUpdateSubject;
        var allowUpdateContact = activity.AllowAIToUpdateContact;

        // Read up front, before the activity is mutated. These are reads that trigger a session flush,
        // and a flush that happens after the activity is dirty tries to persist a stale copy of it -
        // which surfaced as a concurrency conflict whenever the automated expiry pass touched the same
        // activity at the same moment.
        var contactRecord = string.IsNullOrWhiteSpace(activity.ContactContentItemId)
            ? null
            : await contactResolver.FindByIdAsync(activity.ContactContentItemId);

        // The fields the model may set, so it is asked for the exact fields that exist rather than
        // authoring free-form structure the field editors could not read.
        var subjectFieldNames = allowUpdateSubject
            ? await subjectWriter.GetWritableFieldNamesAsync(activity)
            : [];
        var subjectValues = allowUpdateSubject
            ? await subjectWriter.ReadAsync(activity)
            : new Dictionary<string, string>();

        // The prompt lives in Templates/Prompts as a file, like every other system prompt here, so it can be read
        // and changed by someone who is not editing C#. The two guarded sections are passed as variables rather
        // than assembled in code.
        var templateService = services.GetRequiredService<ITemplateService>();

        var systemPrompt = await templateService.RenderAsync(
            VoiceTemplateIds.ConclusionAnalysis,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["AllowSubjectFields"] = allowUpdateSubject && subjectFieldNames.Count > 0,
                ["AllowContactEmail"] = allowUpdateContact,
            });

        var userPrompt = $"""
            Call transcript:
            {transcriptText}

            Subject goal: {flowSettings?.SubjectGoal}

            Available dispositions (choose one DispositionId): {JsonSerializer.Serialize(SubjectDispositionGuidance.Describe(dispositions, allActions, activity.SubjectContentType))}
            """;

        if (subjectFieldNames.Count > 0)
        {
            // Each field is offered with whatever is already on record, so the model is asked to fill
            // gaps rather than to restate what is known.
            var fieldList = subjectFieldNames.Select(name =>
                subjectValues.TryGetValue(name, out var current) && !string.IsNullOrWhiteSpace(current)
                    ? $"{name} (current: {current})"
                    : name);

            userPrompt += $"{Environment.NewLine}{Environment.NewLine}Subject fields you may set (return these keys in SubjectFields): {string.Join("; ", fieldList)}";
        }

        if (allowUpdateContact && contactRecord is not null)
        {
            userPrompt += $"{Environment.NewLine}{Environment.NewLine}Current contact email on file: {contactRecord.GetPrimaryEmail() ?? "(none)"}";
        }

        var conclusionContext = await contextBuilder.BuildAsync(profile, context =>
        {
            context.SystemMessage = systemPrompt;
            context.DisableTools = true;
        });

        var deployment = await deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, deploymentName: conclusionContext.ChatDeploymentName);

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

        // A call with nothing said on it is not analyzed. Asked to review an empty transcript the model does not
        // answer "nothing happened" — it writes a plausible account of a conversation that never occurred, and
        // that account is saved to the CRM as fact. One real call that nobody spoke on came back as "the customer
        // expressed interest in a vehicle but did not specify the type, budget, or timeline", every word of it
        // invented. An unanswered call must read as unanswered.
        VoiceConclusionResult result = null;

        if (hasConversation)
        {
            var response = await client.GetResponseAsync<VoiceConclusionResult>(messages, jsonOptions.SerializerOptions);
            result = response.Result;
        }

        // Decide the disposition and summary from the (read-only) analysis before touching the activity.
        var disposition = VoiceCallConclusionPolicy.ChooseDisposition(dispositions, result?.DispositionId);
        var dispositionId = disposition?.ItemId;

        var notes = VoiceCallConclusionPolicy.ResolveNotes(hasConversation, result?.Summary);

        // Terminal write. Reload the activity fresh (the analysis above ran a slow LLM call, during which the row
        // may have moved on) and apply the conclusion. The answered call was advanced to InProgress, which keeps
        // the automated no-response expiry pass — the only other writer of these rows — out of this window, so
        // this write has no competitor. The explicit flush still surfaces any residual conflict here (logged)
        // rather than letting it blow up at the deferred-scope commit outside this try/catch.
        var concluded = await store.FindByIdAsync(activityId);

        if (concluded is null || concluded.Status.IsTerminal())
        {
            return;
        }

        concluded.Status = ActivityStatus.Completed;
        concluded.CompletedUtc = timeProvider.GetUtcNow().UtcDateTime;

        // Notes and disposition are written together in this single terminal update, so a concluded call is never
        // dispositioned without notes: the notes fall back to a default line when the model returns no summary.
        concluded.Notes = notes;
        concluded.DispositionId = dispositionId;

        // Gated subject write-back: only when the inventory-load guard allowed it and the model returned values for
        // known fields. Each value is written into the field's real structure (a TextField's Text property) rather
        // than merging a model-authored content item, which produced shapes the field editors could not read. The
        // subject lives on the activity, so it must be applied before the activity is persisted below.
        if (allowUpdateSubject)
        {
            await subjectWriter.ApplyAsync(concluded, result?.SubjectFields);
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
        // build a correctly structured EmailAddress), we upsert only a captured email into the ContactMethods bag.
        if (allowUpdateContact && contactRecord is not null &&
            await contactWriter.ApplyAsync(contactRecord.Id, new OmnichannelContactChanges { Email = result?.ContactEmail }))
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("AI voice activity '{ActivityId}' saved a customer-provided email to the contact.", activityId.SanitizeLogValue());
            }
        }

        if (disposition is not null)
        {
            await executor.ExecuteAsync(new SubjectActionExecutionContext
            {
                Activity = concluded,

                // The contact is named on the activity and the subject is carried on it, so the
                // executor resolves both from there rather than being handed content items.
                Disposition = disposition,
            });
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Concluded AI voice activity '{ActivityId}' with disposition '{Disposition}'.", activityId.SanitizeLogValue(), disposition?.Name.SanitizeLogValue());
        }
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
