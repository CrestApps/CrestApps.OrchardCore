using CrestApps.Core.Omnichannel.Services;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI;
using CrestApps.Core.Hosting.Background;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Sms.Handlers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Modules;
using OrchardCore.Sms;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Services;

/// <summary>
/// Proactively re-engages automated SMS contacts who have gone quiet, when the loading campaign enabled it.
/// </summary>
public sealed class SmsReEngagementCycle : ISmsReEngagementCycle
{
    private const int _leaseMilliseconds = 300_000;
    private const int _batchSize = 100;
    private const int _maxConversationsPerInvocation = 200;

    private const string ReEngagementSystemPromptPrefix =
        """
        You are the sales agent in an ongoing SMS conversation with a customer who has not replied to your last message.
        Write a brief, friendly follow-up that re-engages them and invites a response. Follow this guidance from the
        campaign:
        """;

    private const string ReEngagementSystemPromptSuffix =
        """
        Keep it short (one or two sentences), natural, and do not repeat your previous message word for word. Reply with
        only the message text to send — no preamble, quotes, or labels.
        """;

    private static string BuildReEngagementSystemMessage(string guidance)
        => string.IsNullOrWhiteSpace(guidance)
            ? $"{ReEngagementSystemPromptPrefix}\n{ReEngagementSystemPromptSuffix}"
            : $"{ReEngagementSystemPromptPrefix} {guidance.Trim()}\n{ReEngagementSystemPromptSuffix}";

    private readonly ILogger _logger;
    private readonly ISession _session;
    private readonly TimeProvider _timeProvider;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IAIProfileManager _profileManager;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAICompletionContextBuilder _contextBuilder;
    private readonly IAICompletionService _completionService;
    private readonly ICatalog<OmnichannelChannelEndpoint> _endpointCatalog;
    private readonly ICatalog<Cadence> _cadenceCatalog;
    private readonly IContentManager _contentManager;
    private readonly ISmsService _smsService;
    private readonly IOmnichannelActivityStore _omnichannelActivityStore;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly ILocalLock _localLock;
    private readonly IBusinessHoursGate _businessHoursGate;
    private readonly IAutomatedConversationGate _conversationGate;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsReEngagementCycle"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="session">The session.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="promptStore">The prompt store.</param>
    /// <param name="chatSessionManager">The chat session manager.</param>
    /// <param name="profileManager">The profile manager.</param>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="contextBuilder">The context builder.</param>
    /// <param name="completionService">The completion service.</param>
    /// <param name="endpointCatalog">The endpoint catalog.</param>
    /// <param name="cadenceCatalog">The cadence catalog.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="smsService">The sms service.</param>
    /// <param name="omnichannelActivityStore">The omnichannel activity store.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="localLock">The local lock.</param>
    /// <param name="businessHoursGate">The business hours gate.</param>
    /// <param name="conversationGate">The conversation gate.</param>
    public SmsReEngagementCycle(
        ILogger<SmsReEngagementCycle> logger,
        ISession session,
        TimeProvider timeProvider,
        IAIChatSessionPromptStore promptStore,
        IAIChatSessionManager chatSessionManager,
        IAIProfileManager profileManager,
        IAIDeploymentManager deploymentManager,
        IAICompletionContextBuilder contextBuilder,
        IAICompletionService completionService,
        ICatalog<OmnichannelChannelEndpoint> endpointCatalog,
        ICatalog<Cadence> cadenceCatalog,
        IContentManager contentManager,
        ISmsService smsService,
        IOmnichannelActivityStore omnichannelActivityStore,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        ILocalLock localLock,
        IBusinessHoursGate businessHoursGate,
        IAutomatedConversationGate conversationGate)
    {
        _logger = logger;
        _session = session;
        _timeProvider = timeProvider;
        _promptStore = promptStore;
        _chatSessionManager = chatSessionManager;
        _profileManager = profileManager;
        _deploymentManager = deploymentManager;
        _contextBuilder = contextBuilder;
        _completionService = completionService;
        _endpointCatalog = endpointCatalog;
        _cadenceCatalog = cadenceCatalog;
        _contentManager = contentManager;
        _smsService = smsService;
        _omnichannelActivityStore = omnichannelActivityStore;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _localLock = localLock;
        _businessHoursGate = businessHoursGate;
        _conversationGate = conversationGate;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // The business-hours gate is only registered when a feature provides calendars (ContactCenter). Without it,
        // there is no way to know a contact's hours, so we do not nudge at all rather than risk an after-hours send.

        var deadline = _timeProvider.GetUtcNow().UtcDateTime.AddMilliseconds(_leaseMilliseconds * 0.6);

        long documentId = 0;
        var processedCount = 0;

        while (processedCount < _maxConversationsPerInvocation && _timeProvider.GetUtcNow().UtcDateTime < deadline)
        {
            var activities = await _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(x =>
                    x.Status == ActivityStatus.AwaitingCustomerAnswer &&
                    x.InteractionType == ActivityInteractionType.Automated &&
                    x.Channel == OmnichannelConstants.Channels.Sms &&
                    x.DocumentId > documentId,
                    collection: OmnichannelConstants.CollectionName)
                .OrderBy(x => x.DocumentId)
                .Take(_batchSize)
                .ListAsync(cancellationToken);

            if (!activities.Any())
            {
                break;
            }

            foreach (var activity in activities)
            {
                if (_timeProvider.GetUtcNow().UtcDateTime >= deadline)
                {
                    return;
                }

                documentId = activity.Id;
                processedCount++;

                try
                {
                    await TryReEngageAsync(
                        activity,
                        _timeProvider,
                        _chatSessionManager,
                        _promptStore,
                        _profileManager,
                        _deploymentManager,
                        _contextBuilder,
                        _completionService,
                        _endpointCatalog,
                        _cadenceCatalog,
                        _contentManager,
                        _smsService,
                        _omnichannelActivityStore,
                        _subjectFlowSettingsService,
                        _localLock,
                        _businessHoursGate,
                        _conversationGate,
                        _logger,
                        cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to re-engage the automated SMS contact for Activity {ActivityId}.", activity.ItemId.SanitizeLogValue());
                }
            }
        }
    }

    private static async Task TryReEngageAsync(
        OmnichannelActivity activity,
        TimeProvider _timeProvider,
        IAIChatSessionManager _chatSessionManager,
        IAIChatSessionPromptStore _promptStore,
        IAIProfileManager _profileManager,
        IAIDeploymentManager _deploymentManager,
        IAICompletionContextBuilder _contextBuilder,
        IAICompletionService _completionService,
        ICatalog<OmnichannelChannelEndpoint> _endpointCatalog,
        ICatalog<Cadence> _cadenceCatalog,
        IContentManager _contentManager,
        ISmsService _smsService,
        IOmnichannelActivityStore _omnichannelActivityStore,
        ISubjectFlowSettingsService _subjectFlowSettingsService,
        ILocalLock _localLock,
        IBusinessHoursGate _businessHoursGate,
        IAutomatedConversationGate _conversationGate,
        ILogger _logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activity.CadenceId) ||
            string.IsNullOrWhiteSpace(activity.AISessionId) ||
            string.IsNullOrEmpty(activity.ChannelEndpointId))
        {
            return;
        }

        var cadence = await _cadenceCatalog.FindByIdAsync(activity.CadenceId, cancellationToken);

        // No schedule, disabled, or already past its last step: nothing more to send. The step count caps the nudges.
        if (cadence is null ||
            !cadence.Enabled ||
            cadence.Steps is not { Count: > 0 } ||
            activity.ReEngagementAttempts >= cadence.Steps.Count)
        {
            return;
        }

        var step = cadence.Steps[activity.ReEngagementAttempts];

        // A live inbound is composing a reply for this conversation right now; the customer is active, so leave it.
        if (_conversationGate.IsGenerating(activity.AISessionId))
        {
            return;
        }

        var chatSession = await _chatSessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);

        if (chatSession is null)
        {
            return;
        }

        var prompts = (await _promptStore.GetPromptsAsync(chatSession.SessionId))
            .Where(x => !x.IsGeneratedPrompt)
            .ToList();

        // A nudge is only warranted when we are the ones waiting on the customer: the last message must be ours. If
        // the last message is the customer's, a reply is owed instead (handled by the live path / recovery), not a nudge.
        var lastPrompt = prompts.LastOrDefault();

        if (lastPrompt is null || lastPrompt.Role != ChatRole.Assistant)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // The silence threshold for THIS nudge is the current step's DelayMinutes. Because our last message time
        // (_session LastActivityUtc) is updated on every send — the opening and each nudge — this measures the gap since
        // the previous outbound, so successive steps space nudges out and the number of steps caps the total.
        if (step.DelayMinutes <= 0 ||
            chatSession.LastActivityUtc > now.AddMinutes(-step.DelayMinutes))
        {
            return;
        }

        var endpoint = await _endpointCatalog.FindByIdAsync(activity.ChannelEndpointId, cancellationToken);

        if (endpoint is null ||
            !string.Equals(endpoint.Channel, activity.Channel, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(endpoint.Value))
        {
            return;
        }

        // Resolve the contact's local time zone so business hours are evaluated where the customer actually is.
        string contactTimeZoneId = null;
        var contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

        if (contact is not null)
        {
            contactTimeZoneId = contact.Get<OmnichannelContactPart>(nameof(OmnichannelContactPart))?.TimeZoneId;
        }

        // A follow-up is a message like any other, and this task never asked whether it was still welcome. The
        // conversation it is reviving began when the contact was willing to hear from us; somebody who has since
        // said stop is exactly the person a cadence would otherwise keep messaging, on a schedule, for days.
        if (OmnichannelContactPreferences.HasOptedOut(ContentItemOmnichannelContactProjection.Project(contact), activity.Channel))
        {
            return;
        }

        // Every send here is background-initiated, so it must respect business hours. An activity that names a
        // calendar nothing can evaluate — the business-hours feature is off, or the calendar was deleted — is
        // declined rather than nudged, because the alternative is an after-hours message sent on the strength of
        // a calendar nobody can read.
        if (!string.IsNullOrWhiteSpace(activity.BusinessHoursCalendarId))
        {
            var calendars = await _businessHoursGate.GetCalendarOptionsAsync(cancellationToken);

            if (!calendars.Any(calendar => string.Equals(calendar.Id, activity.BusinessHoursCalendarId, StringComparison.OrdinalIgnoreCase)))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Skipping SMS re-engagement for Activity {ActivityId}: its business-hours calendar cannot be evaluated.", activity.ItemId.SanitizeLogValue());
                }

                return;
            }
        }

        if (!await _businessHoursGate.IsOpenAsync(activity.BusinessHoursCalendarId, now, contactTimeZoneId, cancellationToken))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Skipping SMS re-engagement for Activity {ActivityId}: outside business hours.", activity.ItemId.SanitizeLogValue());
            }

            return;
        }

        var profileId = string.IsNullOrWhiteSpace(chatSession.ProfileId) ? activity.AIProfileId : chatSession.ProfileId;

        if (string.IsNullOrWhiteSpace(profileId))
        {
            return;
        }

        var profile = await _profileManager.FindByIdAsync(profileId, cancellationToken);

        if (profile is null || profile.Type != AIProfileType.Chat)
        {
            return;
        }

        // Serialize against the live reply path on the same per-conversation lock. If a reply is being sent we simply
        // skip this pass; the nudge is retried next run.
        var (locker, locked) = await _localLock.TryAcquireLockAsync(
            $"SMS_CONVERSATION_{chatSession.SessionId}",
            TimeSpan.Zero,
            TimeSpan.FromMinutes(2));

        if (!locked)
        {
            return;
        }

        await using (locker)
        {
            // Re-read under the lock: a live reply may have just answered, or another node may have nudged.
            var currentActivity = await _omnichannelActivityStore.FindByIdAsync(activity.ItemId, cancellationToken);

            if (currentActivity is null ||
                currentActivity.Status != ActivityStatus.AwaitingCustomerAnswer ||
                currentActivity.ReEngagementAttempts >= cadence.Steps.Count)
            {
                return;
            }

            var latestPrompts = (await _promptStore.GetPromptsAsync(chatSession.SessionId))
                .Where(x => !x.IsGeneratedPrompt)
                .ToList();

            if (latestPrompts.LastOrDefault()?.Role != ChatRole.Assistant)
            {
                // The customer replied in the meantime; nothing to nudge.
                return;
            }

            string message;

            if (step.IsAiGenerated)
            {
                // The AI composes the nudge from the conversation, guided by the step's optional instruction.
                var transcript = latestPrompts
                    .Select(prompt => new ChatMessage(prompt.Role, prompt.Content))
                    .ToList();

                var context = await _contextBuilder.BuildAsync(profile, ctx =>
                {
                    ctx.SystemMessage = BuildReEngagementSystemMessage(step.Message);
                    ctx.DisableTools = true;
                }, cancellationToken);
                context.AdditionalProperties["Session"] = chatSession;

                var deployment = await _deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, deploymentName: context.ChatDeploymentName, cancellationToken: cancellationToken);

                if (deployment is null)
                {
                    return;
                }

                var completion = await _completionService.CompleteAsync(deployment, transcript, context, cancellationToken);
                message = completion?.Messages?.FirstOrDefault()?.Text?.Trim();
            }
            else
            {
                // A defined-message step sends its verbiage exactly as written.
                message = step.Message?.Trim();
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("The re-engagement step produced no content for Activity {ActivityId}; skipping.", activity.ItemId.SanitizeLogValue());

                return;
            }

            var result = await _smsService.SendAsync(new SmsMessage
            {
                To = currentActivity.PreferredDestination,
                From = endpoint.Value,
                Body = message,
            }, cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogWarning("The SMS provider reported a failed re-engagement send for Activity {ActivityId}.", activity.ItemId.SanitizeLogValue());

                return;
            }

            await _promptStore.CreateAsync(new AIChatSessionPrompt
            {
                ItemId = UniqueId.GenerateId(),
                SessionId = chatSession.SessionId,
                Role = ChatRole.Assistant,
                Content = message,
                CreatedUtc = now,
            }, cancellationToken);

            chatSession.LastActivityUtc = now;
            await _chatSessionManager.SaveAsync(chatSession, cancellationToken);

            currentActivity.ReEngagementAttempts++;
            currentActivity.LastReEngagementUtc = now;

            // Restart the no-response window so the contact gets the full timeout to answer the nudge before the
            // conversation is failed.
            var flowSettings = await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(currentActivity.SubjectContentType, cancellationToken);

            if (flowSettings is not null && OmnichannelAutomationHelper.HasNoResponseTimeout(flowSettings))
            {
                currentActivity.ScheduledUtc = OmnichannelAutomationHelper.ResolveNoResponseDeadline(flowSettings, now);
            }

            await _omnichannelActivityStore.UpdateAsync(currentActivity, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Sent SMS re-engagement {Attempt}/{Max} for Activity {ActivityId}.", currentActivity.ReEngagementAttempts, cadence.Steps.Count, activity.ItemId.SanitizeLogValue());
            }
        }
    }
}
