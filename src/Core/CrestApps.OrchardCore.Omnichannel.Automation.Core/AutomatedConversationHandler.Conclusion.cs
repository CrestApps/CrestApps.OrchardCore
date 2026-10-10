using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Locking;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Automation;

public sealed partial class AutomatedConversationHandler
{
    // After a handled turn, the conversation is judged in a deferred task, so the reply is not held back by the analysis.
    // The analysis runs under the conversation's lock, so it never interleaves with the next turn's reads and writes.
    private void ScheduleConclusion(AutomatedConversationTurn turn, bool hangupRequested)
    {
        var activityId = turn.Activity.ItemId;
        var sessionId = turn.ChatSession.SessionId;
        var channel = turn.Channel;
        var profile = turn.Profile;
        var flowSettings = turn.FlowSettings;

        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;
            var localLock = services.GetRequiredService<ILocalLock>();

            var (locker, locked) = await localLock.TryAcquireLockAsync(GetLockKey(sessionId), _lockTimeout, _lockExpiration);

            if (!locked)
            {
                return;
            }

            await using var lockScope = locker;

            try
            {
                await ConcludeAsync(services, channel, activityId, sessionId, profile, flowSettings, hangupRequested);
            }
            catch (Exception ex)
            {
                services.GetRequiredService<ILogger<AutomatedConversationHandler>>()
                    .LogError(ex, "The conclusion analysis of automated Activity {ActivityId} failed.", activityId.SanitizeLogValue());
            }
        });
    }

    private async Task ConcludeAsync(
        IServiceProvider services,
        IAutomatedMessagingChannel channel,
        string activityId,
        string sessionId,
        CrestApps.Core.AI.Models.AIProfile profile,
        SubjectFlowSettings flowSettings,
        bool hangupRequested)
    {
        var store = services.GetRequiredService<IOmnichannelActivityStore>();
        var activity = await store.FindByIdAsync(activityId);

        if (activity is null || activity.Status.IsTerminal())
        {
            return;
        }

        var actionCatalog = services.GetRequiredService<ISourceCatalog<SubjectAction>>();
        var dispositionCatalog = services.GetRequiredService<ICatalog<OmnichannelDisposition>>();
        var clientFactory = services.GetRequiredService<IAIClientFactory>();
        var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();
        var contextBuilder = services.GetRequiredService<IAICompletionContextBuilder>();
        var promptStore = services.GetRequiredService<IAIChatSessionPromptStore>();
        var templateService = services.GetRequiredService<ITemplateService>();
        var contentManager = services.GetRequiredService<IContentManager>();
        var contentDefinitionManager = services.GetRequiredService<IContentDefinitionManager>();
        var clock = services.GetRequiredService<IClock>();
        var logger = services.GetRequiredService<ILogger<AutomatedConversationHandler>>();

        var allActions = await actionCatalog.GetAllAsync();
        var subjectDispositionIds = allActions
            .Where(action => string.Equals(action.SubjectContentType, activity.SubjectContentType, StringComparison.OrdinalIgnoreCase))
            .Select(action => action.DispositionId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();
        var dispositions = await dispositionCatalog.GetAsync(subjectDispositionIds);

        var conclusionPrompt = await templateService.RenderAsync(channel.ConclusionPromptTemplateId);
        var conclusionContext = await contextBuilder.BuildAsync(profile, context =>
        {
            context.SystemMessage = conclusionPrompt;
            context.DisableTools = true;
        });

        var deployment = await deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, deploymentName: conclusionContext.ChatDeploymentName);

        if (deployment is null)
        {
            return;
        }

        var client = await clientFactory.CreateChatClientAsync(deployment, builder => builder.UseDefaultResilience().UseUsageLabels(contextType: channel.UsageCategory, purpose: AIUsageFeaturePurposes.ConversationConclusion));

        ContentItem subject = null;
        ContentItem contact = null;

        // The subject's text fields are the only structure the model may set: it is shown their keys ("Part.Field") and
        // returns values, rather than authoring a content item.
        var subjectTextFields = activity.AllowAIToUpdateSubject && !string.IsNullOrWhiteSpace(activity.SubjectContentType)
            ? OmnichannelSubjectWriter.GetSubjectTextFields(await contentDefinitionManager.GetTypeDefinitionAsync(activity.SubjectContentType))
            : [];

        var sessionPrompts = await promptStore.GetPromptsAsync(sessionId);

        // The load may let the AI convert a lead it qualified. Only an open lead can be converted, so the record is read
        // to check, and the model is told it is talking to a lead only then.
        LeadAIConversionSettings leadConversion = null;

        if (activity.TryGet<LeadAIConversionSettings>(out var storedLeadConversion) && storedLeadConversion.Enabled)
        {
            contact ??= await GetContactAsync(contentManager, activity);

            if (!LeadAIConversion.TryGetSettings(activity, contact, out leadConversion))
            {
                leadConversion = null;
            }
        }

        var userPrompt = $"""

            Current UTC time: {clock.UtcNow:O}
            Chat Summary: {JsonSerializer.Serialize(sessionPrompts)}
            Subject Goal: {flowSettings.SubjectGoal}
            List of Dispositions: {JsonSerializer.Serialize(SubjectDispositionGuidance.Describe(dispositions, allActions, activity.SubjectContentType))}

            Decide whether the conversation has genuinely ended. Set Concluded to true ONLY when the exchange is clearly over: the agent has said goodbye or sent a closing message, or the customer has opted out, declined, or stopped engaging. Do NOT conclude while the agent is still asking a question or waiting for the customer to answer or confirm something (for example, right after the agent asked "is that correct?" the conversation is NOT concluded). When Concluded is true, select the single best DispositionId from the list above.
            Always return Notes: a concise plain-text summary of the outcome to store on the account.
            If, and only if, the customer clearly agreed to be contacted again at a specific time, set CallbackAtUtc to that moment as an absolute UTC timestamp (ISO 8601), resolving any relative time against the current UTC time above; otherwise leave CallbackAtUtc null.
            If a follow-up is warranted, set NextActivityNotes to short preparation notes for whoever handles the next activity; otherwise leave it null.
            {(subjectTextFields.Count > 0 ? "You are given a list of subject field keys. Return SubjectFields as a JSON object mapping the exact key shown to a short plain-text value, for any field the conversation clearly revealed; omit fields you did not learn and never invent keys." : "Do not return SubjectFields.")}
            {(activity.AllowAIToUpdateContact ? "If, and only if, the customer clearly provided an email address for follow-up, set ContactEmail to that exact address (lowercased, no surrounding words); if it matches the current email on file or none was given, omit ContactEmail." : "Do not return ContactEmail.")}
            {(leadConversion is not null ? LeadAIConversion.BuildInstruction(leadConversion, flowSettings.SubjectGoal) : "Do not return ConvertLead.")}

            """;

        if (subjectTextFields.Count > 0)
        {
            subject ??= await GetSubjectAsync(contentManager, activity);

            userPrompt += $"""

                Subject field keys: {JsonSerializer.Serialize(subjectTextFields.Select(field => $"{field.Part}.{field.Field}"))}
                """;
        }

        if (activity.AllowAIToUpdateContact)
        {
            contact ??= await GetContactAsync(contentManager, activity);

            userPrompt += $"""

                Current contact email: {OmnichannelSubjectWriter.GetContactEmail(contact) ?? "(none)"}
                """;
        }

        var result = await client.GetResponseAsync<ConversationConclusionResult>(
            [
                new ChatMessage(ChatRole.System, conclusionPrompt),
                new ChatMessage(ChatRole.User, userPrompt),
            ],
            _jsonSerializerOptions.SerializerOptions);

        if (result.Result is null)
        {
            return;
        }

        if (activity.AllowAIToUpdateSubject && subject is not null &&
            OmnichannelSubjectWriter.ApplySubjectFields(subject, result.Result.SubjectFields, subjectTextFields))
        {
            activity.Subject = subject;

            // The subject is kept even when the conversation has not concluded.
            await store.UpdateAsync(activity);
        }

        if (activity.AllowAIToUpdateContact && contact is not null &&
            await OmnichannelSubjectWriter.TryApplyContactEmailAsync(contentManager, contact, result.Result.ContactEmail))
        {
            await contentManager.UpdateAsync(contact);
        }

        if (!result.Result.Concluded && !hangupRequested)
        {
            return;
        }

        if (flowSettings.RequireDisposition && string.IsNullOrEmpty(result.Result.DispositionId))
        {
            logger.LogWarning("The automated conversation of Activity {ActivityId} reported concluded without a disposition, but its subject flow requires one; it stays open until the no-response timeout or an opt-out closes it.", activity.ItemId.SanitizeLogValue());

            return;
        }

        activity.Status = ActivityStatus.Completed;
        activity.CompletedUtc = clock.UtcNow;
        activity.DispositionId = result.Result.DispositionId;
        activity.CompletedById = activity.AssignedToId;
        activity.CompletedByUsername = activity.AssignedToUsername;
        ActivityDispositionActors.Stamp(activity, ActivityDispositionActor.AIAgent);

        // A concluded conversation is always notated, as the voice and SMS channels do.
        activity.Notes = string.IsNullOrWhiteSpace(result.Result.Notes)
            ? $"Automated AI {channel.ConversationNoun} completed."
            : result.Result.Notes.Trim();

        await store.UpdateAsync(activity);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Concluded automated {Channel} Activity {ActivityId} with disposition {DispositionId}.", channel.Channel, activity.ItemId.SanitizeLogValue(), (result.Result.DispositionId ?? "(none)").SanitizeLogValue());
        }

        subject ??= await GetSubjectAsync(contentManager, activity);
        contact ??= await GetContactAsync(contentManager, activity);

        // The AI judged the lead qualified and the load allows it to convert: the lead becomes a contact before the
        // disposition's actions run, so follow-ups land on the contact.
        if (leadConversion is not null && result.Result.ConvertLead == true)
        {
            contact = await ConvertQualifiedLeadAsync(services, activity, contact, leadConversion, logger) ?? contact;
        }

        var disposition = dispositions.FirstOrDefault(candidate => candidate.ItemId == result.Result.DispositionId);

        // Nothing to act on (no disposition, or an id outside the offered list) is a conversation with no follow-up.
        if (disposition is null)
        {
            return;
        }

        var (scheduleDates, preparationNotes) = MapFollowUps(allActions, activity, disposition, result.Result);

        await services.GetRequiredService<ISubjectActionExecutor>().ExecuteAsync(new SubjectActionExecutionContext
        {
            Activity = activity,
            Contact = contact,
            Subject = subject,
            Disposition = disposition,
            ActionScheduleDates = scheduleDates,
            ActionPreparationNotes = preparationNotes,
        });
    }

    // Carries the callback time and follow-up notes the AI found onto every follow-up action (Try again, New activity) of
    // the chosen disposition. When the AI returned neither, the executor keeps its own defaults.
    private static (Dictionary<string, DateTime?> ScheduleDates, Dictionary<string, string> PreparationNotes) MapFollowUps(
        IEnumerable<SubjectAction> allActions,
        OmnichannelActivity activity,
        OmnichannelDisposition disposition,
        ConversationConclusionResult result)
    {
        if (!result.CallbackAtUtc.HasValue && string.IsNullOrWhiteSpace(result.NextActivityNotes))
        {
            return (null, null);
        }

        var followUpActionIds = allActions
            .Where(action => string.Equals(action.SubjectContentType, activity.SubjectContentType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action.DispositionId, disposition.ItemId, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(action.Source, OmnichannelConstants.ActionTypes.NewActivity, StringComparison.OrdinalIgnoreCase)))
            .Select(action => action.ItemId)
            .ToList();

        if (followUpActionIds.Count == 0)
        {
            return (null, null);
        }

        Dictionary<string, DateTime?> scheduleDates = null;
        Dictionary<string, string> preparationNotes = null;

        if (result.CallbackAtUtc.HasValue)
        {
            var callbackUtc = DateTime.SpecifyKind(result.CallbackAtUtc.Value, DateTimeKind.Utc);
            scheduleDates = followUpActionIds.ToDictionary(id => id, _ => (DateTime?)callbackUtc);
        }

        if (!string.IsNullOrWhiteSpace(result.NextActivityNotes))
        {
            var notes = result.NextActivityNotes.Trim();
            preparationNotes = followUpActionIds.ToDictionary(id => id, _ => notes);
        }

        return (scheduleDates, preparationNotes);
    }

    private static async Task<ContentItem> ConvertQualifiedLeadAsync(
        IServiceProvider services,
        OmnichannelActivity activity,
        ContentItem lead,
        LeadAIConversionSettings settings,
        ILogger logger)
    {
        // Without the CRM feature there is nothing to convert with.
        var converter = services.GetService<IUnattendedLeadConverter>();

        if (converter is null || lead is null)
        {
            return null;
        }

        var result = await converter.ConvertAsync(lead, LeadAIConversion.CreateRequest(activity, settings));

        if (!result.Succeeded)
        {
            logger.LogWarning("The AI qualified the lead of automated activity {ActivityId}, but the lead could not be converted: {Errors}", activity.ItemId.SanitizeLogValue(), string.Join(" ", result.Errors).SanitizeLogValue());

            return null;
        }

        return result.Contact;
    }

    private sealed class ConversationConclusionResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the conversation has concluded.
        /// </summary>
        public bool Concluded { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the chosen disposition.
        /// </summary>
        public string DispositionId { get; set; }

        /// <summary>
        /// Gets or sets a concise summary of the outcome, stored as the activity's notes.
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// Gets or sets the absolute UTC time the customer agreed to be contacted again, when one was established.
        /// </summary>
        public DateTime? CallbackAtUtc { get; set; }

        /// <summary>
        /// Gets or sets short preparation notes for the follow-up activity, when one is warranted.
        /// </summary>
        public string NextActivityNotes { get; set; }

        /// <summary>
        /// Gets or sets the subject field values the AI captured, keyed by "Part.Field", when allowed.
        /// </summary>
        public Dictionary<string, string> SubjectFields { get; set; }

        /// <summary>
        /// Gets or sets the email address the customer gave for follow-up, when allowed.
        /// </summary>
        public string ContactEmail { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the AI judged the lead qualified and asks to convert it, when allowed.
        /// </summary>
        public bool? ConvertLead { get; set; }
    }
}
