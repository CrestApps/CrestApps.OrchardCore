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
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
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
        //
        // A voicemail is not a conversation either, whatever its transcript holds: the "customer" in it is a
        // recording. Reviewed, one greeting was read as the customer declining and concluded as do-not-call, opting
        // out somebody who had never picked up. Recognised from the transcript as well as from the marker, so a call
        // held by a live session, which leaves no marker, is judged the same way.
        var reachedVoicemail = VoicemailGreeting.ReachedVoicemail(
            activity.TryGet<VoicemailReached>(out var voicemail) ? voicemail : null,
            sessionPrompts);
        var hasConversation = VoiceCallConclusionPolicy.HasConversation(sessionPrompts) && !reachedVoicemail;

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
            ? OmnichannelSubjectWriter.GetSubjectTextFields(await definitionManager.GetTypeDefinitionAsync(activity.SubjectContentType))
            : [];

        // The prompt lives in Templates/Prompts as a file, like every other system prompt here, so it can be read
        // and changed by someone who is not editing C#. The two guarded sections are passed as variables rather
        // than assembled in code.
        var templateService = services.GetRequiredService<ITemplateService>();

        var systemPrompt = await templateService.RenderAsync(
            VoiceTemplateIds.ConclusionAnalysis,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["AllowSubjectFields"] = allowUpdateSubject && subjectTextFields.Count > 0,
                ["AllowContactEmail"] = allowUpdateContact,
            });

        var userPrompt = $"""
            Call transcript:
            {transcriptText}

            Subject goal: {flowSettings?.SubjectGoal}

            Available dispositions (choose one DispositionId): {JsonSerializer.Serialize(SubjectDispositionGuidance.Describe(dispositions, allActions, activity.SubjectContentType))}
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
            userPrompt += $"{Environment.NewLine}{Environment.NewLine}Current contact email on file: {OmnichannelSubjectWriter.GetContactEmail(contact) ?? "(none)"}";
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
            var response = await client.GetResponseAsync<VoiceConclusionResult>(
                messages,
                jsonOptions.SerializerOptions,
                await CreateReviewOptionsAsync(services, activity.AISessionId));
            result = response.Result;
        }

        // Decide the disposition and summary from the (read-only) analysis before touching the activity.
        // A call nobody spoke on is not the model's to judge, and takes the outcome that tries again later.
        var disposition = hasConversation
            ? VoiceCallConclusionPolicy.ChooseDisposition(dispositions, result?.DispositionId)
            : VoiceCallConclusionPolicy.ChooseUnansweredDisposition(dispositions, allActions, activity.SubjectContentType);
        var dispositionId = disposition?.ItemId;

        var notes = reachedVoicemail
            ? VoiceCallConclusionPolicy.VoicemailNote
            : VoiceCallConclusionPolicy.ResolveNotes(hasConversation, result?.Summary);

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
        concluded.CompletedUtc = clock.UtcNow;

        // Notes and disposition are written together in this single terminal update, so a concluded call is never
        // dispositioned without notes: the notes fall back to a default line when the model returns no summary.
        concluded.Notes = notes;
        concluded.DispositionId = dispositionId;

        // Gated subject write-back: only when the inventory-load guard allowed it and the model returned values for
        // known fields. Each value is written into the field's real structure (a TextField's Text property) rather
        // than merging a model-authored content item, which produced shapes the field editors could not read. The
        // subject lives on the activity, so it must be applied before the activity is persisted below.
        if (allowUpdateSubject && subject is not null && OmnichannelSubjectWriter.ApplySubjectFields(subject, result?.SubjectFields, subjectTextFields))
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
        // build a correctly structured EmailAddress), we upsert only a captured email into the ContactMethods bag.
        if (allowUpdateContact && contact is not null &&
            await OmnichannelSubjectWriter.TryApplyContactEmailAsync(contentManager, contact, result?.ContactEmail))
        {
            await contentManager.UpdateAsync(contact);

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
                Contact = contact,
                Subject = subject,
                Disposition = disposition,
            });
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Concluded AI voice activity '{ActivityId}' with disposition '{Disposition}'.", activityId.SanitizeLogValue(), disposition?.Name.SanitizeLogValue());
        }
    }

    /// <summary>
    /// The options the call review is asked with: the call's own chat session, so usage tracking records the
    /// review's tokens against the call rather than against nothing.
    /// </summary>
    /// <remarks>
    /// The review runs in a deferred scope with no invocation scope around it, so nothing else tells the tracking
    /// client which session it belongs to, and the usage report leaves out every record that belongs to none.
    /// </remarks>
    /// <param name="services">The deferred scope's services.</param>
    /// <param name="sessionId">The call's chat session.</param>
    private static async Task<ChatOptions> CreateReviewOptionsAsync(IServiceProvider services, string sessionId)
    {
        var sessionManager = services.GetService<IAIChatSessionManager>();
        var session = sessionManager is null || string.IsNullOrEmpty(sessionId)
            ? null
            : await sessionManager.FindByIdAsync(sessionId);

        return session is null
            ? null
            : new ChatOptions
            {
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    [AICompletionContextKeys.Session] = session,
                },
            };
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
