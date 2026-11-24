using System.Data.Common;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Locking;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Automation;

/// <summary>
/// Follows up, on the campaign's cadence, with automated contacts who stopped answering, on every automated channel: each
/// cadence step waits its delay after the last message, then sends its own words or an AI-written nudge, within the
/// campaign's business hours and never to a contact who opted out. The number of steps caps the follow-ups.
/// </summary>
/// <remarks>
/// Each follow-up is committed on its own while the conversation lock is held, so a conflicting change to one activity
/// loses that one write instead of rolling back, and later re-sending, every follow-up of the pass. A failed read or
/// write cancels the YesSql session for the rest of the scope, so the pass stops there and the next pass goes on after
/// that conversation.
/// </remarks>
public sealed class AutomatedFollowUpService
{
    private const int BatchSize = 100;
    private const int MaxConversationsPerPass = 200;

    private readonly IEnumerable<IAutomatedMessagingChannel> _channels;
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAIProfileManager _profileManager;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAICompletionContextBuilder _contextBuilder;
    private readonly IAICompletionService _completionService;
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly ICatalog<Cadence> _cadenceCatalog;
    private readonly IContentManager _contentManager;
    private readonly IContactOptOutResolver _optOutResolver;
    private readonly IEnumerable<IOmnichannelSendPacer> _pacers;
    private readonly IOmnichannelActivityStore _activityStore;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly IBusinessHoursGate _businessHoursGate;
    private readonly IAutomatedConversationGate _conversationGate;
    private readonly ILocalLock _localLock;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly AutomatedActivityScanCursors _cursors;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutomatedFollowUpService"/> class.
    /// </summary>
    /// <param name="channels">The automated channels.</param>
    /// <param name="channelResolver">The resolver of the messaging channels follow-ups are sent through.</param>
    /// <param name="chatSessionManager">The AI chat session manager.</param>
    /// <param name="promptStore">The AI chat transcript store.</param>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="deploymentManager">The AI deployment manager.</param>
    /// <param name="contextBuilder">The AI completion context builder.</param>
    /// <param name="completionService">The AI completion service.</param>
    /// <param name="endpointManager">The manager of the business's addresses.</param>
    /// <param name="cadenceCatalog">The cadence catalog.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="optOutResolver">The resolver of a contact's opt-outs.</param>
    /// <param name="pacers">The pacers that hold outreach back while a sending address is at its limit or paused.</param>
    /// <param name="activityStore">The activity store.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="businessHoursGate">The gate that keeps follow-ups within business hours.</param>
    /// <param name="conversationGate">The registry of the replies being composed right now.</param>
    /// <param name="localLock">The lock that serializes a conversation's messages.</param>
    /// <param name="session">The session the activities are read from.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="cursors">Where the previous pass stopped reading the conversations awaiting an answer.</param>
    /// <param name="logger">The logger.</param>
    public AutomatedFollowUpService(
        IEnumerable<IAutomatedMessagingChannel> channels,
        IMessagingChannelResolver channelResolver,
        IAIChatSessionManager chatSessionManager,
        IAIChatSessionPromptStore promptStore,
        IAIProfileManager profileManager,
        IAIDeploymentManager deploymentManager,
        IAICompletionContextBuilder contextBuilder,
        IAICompletionService completionService,
        IOmnichannelChannelEndpointManager endpointManager,
        ICatalog<Cadence> cadenceCatalog,
        IContentManager contentManager,
        IContactOptOutResolver optOutResolver,
        IEnumerable<IOmnichannelSendPacer> pacers,
        IOmnichannelActivityStore activityStore,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IBusinessHoursGate businessHoursGate,
        IAutomatedConversationGate conversationGate,
        ILocalLock localLock,
        ISession session,
        IClock clock,
        AutomatedActivityScanCursors cursors,
        ILogger<AutomatedFollowUpService> logger)
    {
        _channels = channels;
        _channelResolver = channelResolver;
        _chatSessionManager = chatSessionManager;
        _promptStore = promptStore;
        _profileManager = profileManager;
        _deploymentManager = deploymentManager;
        _contextBuilder = contextBuilder;
        _completionService = completionService;
        _endpointManager = endpointManager;
        _cadenceCatalog = cadenceCatalog;
        _contentManager = contentManager;
        _optOutResolver = optOutResolver;
        _pacers = pacers;
        _activityStore = activityStore;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _businessHoursGate = businessHoursGate;
        _conversationGate = conversationGate;
        _localLock = localLock;
        _session = session;
        _clock = clock;
        _cursors = cursors;
        _logger = logger;
    }

    /// <summary>
    /// Sends the follow-ups that are due, until the deadline passes.
    /// </summary>
    /// <param name="deadlineUtc">When to stop, so the background task's lock does not expire mid-pass.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many follow-ups were sent.</returns>
    public async Task<int> SendDueAsync(DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        var channels = _channels.ToDictionary(channel => channel.Channel, StringComparer.OrdinalIgnoreCase);

        if (channels.Count == 0)
        {
            return 0;
        }

        var channelNames = channels.Keys.ToArray();
        var documentId = _cursors.FollowUp;
        var wrapped = documentId == 0;

        // After going round, the pass reads only up to where it started, so no conversation is examined twice.
        var upTo = long.MaxValue;
        var startedAt = documentId;
        var examined = 0;
        var sent = 0;

        while (examined < MaxConversationsPerPass && _clock.UtcNow < deadlineUtc)
        {
            var activities = await _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index =>
                    index.Status == ActivityStatus.AwaitingCustomerAnswer &&
                    index.InteractionType == ActivityInteractionType.Automated &&
                    index.Channel.IsIn(channelNames) &&
                    index.DocumentId > documentId &&
                    index.DocumentId <= upTo,
                    collection: OmnichannelConstants.CollectionName)
                .OrderBy(index => index.DocumentId)
                .Take(BatchSize)
                .ListAsync(cancellationToken);

            if (activities.Count == 0)
            {
                // The end of the list: the next pass starts again from the first conversation, and this one goes
                // round once to reach the conversations before where it started.
                if (wrapped)
                {
                    documentId = 0;

                    break;
                }

                wrapped = true;
                upTo = startedAt;
                documentId = 0;

                continue;
            }

            foreach (var activity in activities)
            {
                // The cap holds within a batch too, so a pass never examines more than its share.
                if (examined >= MaxConversationsPerPass)
                {
                    break;
                }

                if (_clock.UtcNow >= deadlineUtc)
                {
                    _cursors.FollowUp = documentId;

                    return sent;
                }

                documentId = activity.Id;
                examined++;

                try
                {
                    if (channels.TryGetValue(activity.Channel, out var channel) &&
                        await TryFollowUpAsync(channel, activity, cancellationToken))
                    {
                        sent++;
                    }
                }
                catch (ConcurrencyException ex)
                {
                    // A write of this follow-up conflicted with a change made meanwhile: most often the activity, whose
                    // attempt count is then lost while the transcript, committed first, still holds the message; or a record
                    // the send itself wrote. YesSql has cancelled the session, so nothing later in this pass could be saved:
                    // the pass stops here.
                    _logger.LogWarning(ex, "A write of the follow-up of automated Activity {ActivityId} conflicted with a change made meanwhile; the pass stops and the next one goes on after it.", activity.ItemId.SanitizeLogValue());
                    _cursors.FollowUp = documentId;

                    return sent;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Any failed read or write cancels the YesSql session, and a follow-up sent after it would never be
                    // recorded and would go out again on the next pass. The pass stops here; the next one goes on after it.
                    _logger.LogError(ex, "Following up automated Activity {ActivityId} failed; the pass stops and the next one goes on after it.", activity.ItemId.SanitizeLogValue());
                    _cursors.FollowUp = documentId;

                    return sent;
                }
            }
        }

        _cursors.FollowUp = documentId;

        return sent;
    }

    private async Task<bool> TryFollowUpAsync(IAutomatedMessagingChannel channel, OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activity.CadenceId) ||
            string.IsNullOrWhiteSpace(activity.AISessionId) ||
            string.IsNullOrEmpty(activity.ChannelEndpointId) ||
            _conversationGate.IsGenerating(activity.AISessionId))
        {
            return false;
        }

        var cadence = await _cadenceCatalog.FindByIdAsync(activity.CadenceId, cancellationToken);

        // No schedule, disabled, or already past its last step: nothing more to send.
        if (cadence is null || !cadence.Enabled || cadence.Steps is not { Count: > 0 } || activity.ReEngagementAttempts >= cadence.Steps.Count)
        {
            return false;
        }

        var step = cadence.Steps[activity.ReEngagementAttempts];
        var chatSession = await _chatSessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);

        if (chatSession is null || !AutomatedConversationTranscript.IsAwaitingCustomer(await GetConversationAsync(chatSession)))
        {
            // Only when we are the ones waiting on the customer: the agent spoke last, or handled the customer's last
            // message without replying. When a customer message is still owed a reply, the recovery answers it instead.
            return false;
        }

        var now = _clock.UtcNow;

        // The step's delay is measured from our last message, which every send moves, so the steps space the follow-ups.
        if (step.DelayMinutes <= 0 || chatSession.LastActivityUtc > now.AddMinutes(-step.DelayMinutes))
        {
            return false;
        }

        var messagingChannel = _channelResolver.Get(channel.Channel);
        var endpoint = await _endpointManager.FindByIdAsync(activity.ChannelEndpointId, cancellationToken);

        if (messagingChannel is null || endpoint is null || !endpoint.HasCapability(channel.Channel) || string.IsNullOrWhiteSpace(endpoint.Value))
        {
            return false;
        }

        var contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

        // A contact who has since asked to stop is exactly the person a cadence would otherwise keep messaging for days.
        if (await _optOutResolver.HasOptedOutAsync(contact, activity.Channel, cancellationToken))
        {
            return false;
        }

        if (!await IsWithinBusinessHoursAsync(activity, contact, now, cancellationToken))
        {
            return false;
        }

        // A follow-up is outreach: an address at its sending limit or paused waits, and is asked again on a later pass,
        // before anything is composed.
        if (await IsHeldBackAsync(channel.Channel, endpoint, cancellationToken))
        {
            return false;
        }

        var profileId = string.IsNullOrWhiteSpace(chatSession.ProfileId) ? activity.AIProfileId : chatSession.ProfileId;
        var profile = string.IsNullOrWhiteSpace(profileId) ? null : await _profileManager.FindByIdAsync(profileId, cancellationToken);

        if (profile is null || profile.Type != AIProfileType.Chat)
        {
            return false;
        }

        // Serialized with the live reply path; if a reply is being sent this pass skips, and the next one retries.
        var (locker, locked) = await _localLock.TryAcquireLockAsync(AutomatedConversationHandler.GetLockKey(chatSession.SessionId), TimeSpan.Zero, AutomatedConversationHandler.LockExpiration);

        if (!locked)
        {
            return false;
        }

        await using (locker)
        {
            // Read again under the lock, fresh from the database: the batch query left its copy in the session, and YesSql
            // would hand that back. A live reply may have just answered, the conclusion closed the conversation, or another
            // pass already followed up, which changes the step to send.
            var attempts = activity.ReEngagementAttempts;

            _session.Detach(activity, OmnichannelConstants.CollectionName);

            var current = await _activityStore.FindByIdAsync(activity.ItemId, cancellationToken);
            var conversation = await GetConversationAsync(chatSession);

            if (current is null ||
                current.Status != ActivityStatus.AwaitingCustomerAnswer ||
                current.ReEngagementAttempts != attempts ||
                !AutomatedConversationTranscript.IsAwaitingCustomer(conversation) ||
                conversation[conversation.Count - 1].CreatedUtc > now.AddMinutes(-step.DelayMinutes))
            {
                return false;
            }

            var message = step.IsAiGenerated
                ? await ComposeAsync(channel, profile, chatSession, conversation, step.Message, cancellationToken)
                : step.Message?.Trim();

            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("The follow-up step produced no content for Activity {ActivityId}; skipping.", activity.ItemId.SanitizeLogValue());

                return false;
            }

            // A follow-up continues the thread the opening message started, under its subject.
            var thread = current.TryGet<AutomatedConversationThread>(out var stored) ? stored : null;

            var result = await messagingChannel.SendAsync(new MessagingOutboundMessage
            {
                ServiceAddress = endpoint.Value,
                ContactAddress = current.PreferredDestination,
                Purpose = MessagingOutboundPurpose.Outreach,
                Subject = MessagingSubjects.ForReply(thread?.Subject),
                Body = message,
            }, cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogWarning("The {Channel} provider refused the follow-up for Activity {ActivityId}: {Error}", channel.Channel, activity.ItemId.SanitizeLogValue(), result.GetErrorText().SanitizeLogValue());

                return false;
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

            // The message went out, so the transcript records it now, with whatever the send itself wrote, before the
            // activity write below: a conflicting change to the activity then cannot erase the record of what the customer
            // was sent, and the next follow-up still waits its step's delay.
            await _session.SaveChangesAsync(cancellationToken);

            current.ReEngagementAttempts++;
            current.LastReEngagementUtc = now;

            // The contact gets the full no-response window to answer the follow-up before the conversation is failed.
            var flowSettings = string.IsNullOrWhiteSpace(current.SubjectContentType)
                ? null
                : await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(current.SubjectContentType, cancellationToken);

            if (flowSettings is not null && OmnichannelAutomationHelper.HasNoResponseTimeout(flowSettings))
            {
                current.ScheduledUtc = OmnichannelAutomationHelper.ResolveNoResponseDeadline(flowSettings, now);
            }

            await _activityStore.UpdateAsync(current, cancellationToken);

            // Committed while the lock is held, one follow-up at a time.
            await _session.SaveChangesAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Sent {Channel} follow-up {Attempt}/{Max} for Activity {ActivityId}.", channel.Channel, current.ReEngagementAttempts, cadence.Steps.Count, activity.ItemId.SanitizeLogValue());
            }

            return true;
        }
    }

    // Every send here starts unprompted, so it keeps to business hours in the contact's time zone. An activity naming a
    // calendar nothing can evaluate is declined rather than followed up out of hours.
    private async Task<bool> IsHeldBackAsync(string channelName, OmnichannelChannelEndpoint endpoint, CancellationToken cancellationToken)
    {
        foreach (var pacer in _pacers)
        {
            if (string.Equals(pacer.Channel, channelName, StringComparison.OrdinalIgnoreCase) &&
                await pacer.GetBulkSendTimeAsync(endpoint, reserveTurn: false, cancellationToken) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> IsWithinBusinessHoursAsync(OmnichannelActivity activity, ContentItem contact, DateTime now, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(activity.BusinessHoursCalendarId))
        {
            var calendars = await _businessHoursGate.GetCalendarOptionsAsync(cancellationToken);

            if (!calendars.Any(calendar => string.Equals(calendar.Id, activity.BusinessHoursCalendarId, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        var timeZoneId = contact is not null && contact.TryGet<OmnichannelContactPart>(out var contactPart)
            ? contactPart.TimeZoneId
            : null;

        return await _businessHoursGate.IsOpenAsync(activity.BusinessHoursCalendarId, now, timeZoneId, cancellationToken);
    }

    // A failed model call skips this follow-up and the pass goes on. A database failure is not swallowed: it cancels the
    // session, and the pass must stop rather than send follow-ups it can no longer record.
    private async Task<string> ComposeAsync(IAutomatedMessagingChannel channel, AIProfile profile, AIChatSession chatSession, IReadOnlyList<AIChatSessionPrompt> conversation, string guidance, CancellationToken cancellationToken)
    {
        try
        {
            return await ComposeCoreAsync(channel, profile, chatSession, conversation, guidance, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not DbException)
        {
            _logger.LogWarning(ex, "Composing the AI follow-up for session {SessionId} failed.", chatSession.SessionId.SanitizeLogValue());

            return null;
        }
    }

    private async Task<string> ComposeCoreAsync(IAutomatedMessagingChannel channel, AIProfile profile, AIChatSession chatSession, IReadOnlyList<AIChatSessionPrompt> conversation, string guidance, CancellationToken cancellationToken)
    {
        var transcript = conversation
            .Select(prompt => new ChatMessage(prompt.Role, prompt.Content))
            .ToList();

        var systemMessage = $"""
            You are the agent in an ongoing {channel.ConversationNoun} with a customer who has not replied to your last message.
            Write a brief, friendly follow-up that re-engages them and invites a response. {(string.IsNullOrWhiteSpace(guidance) ? string.Empty : $"Follow this guidance from the campaign: {guidance.Trim()}")}
            Keep it short, natural, and do not repeat your previous message word for word. Reply with only the message text to
            send, with no preamble, quotes, or labels.
            """;

        var context = await _contextBuilder.BuildAsync(profile, builder =>
        {
            builder.SystemMessage = systemMessage;
            builder.DisableTools = true;
        }, cancellationToken);
        context.AdditionalProperties["Session"] = chatSession;

        var deployment = await _deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, deploymentName: context.ChatDeploymentName, cancellationToken: cancellationToken);

        if (deployment is null)
        {
            return null;
        }

        using var usageScope = AIUsageScope.Begin(contextType: channel.UsageCategory, purpose: AIUsageFeaturePurposes.ReEngagement);
        var completion = await _completionService.CompleteAsync(deployment, transcript, context, cancellationToken);

        return completion?.Messages?.FirstOrDefault()?.Text?.Trim();
    }

    private async Task<IReadOnlyList<AIChatSessionPrompt>> GetConversationAsync(AIChatSession chatSession)
        => (await _promptStore.GetPromptsAsync(chatSession.SessionId))
            .Where(prompt => !prompt.IsGeneratedPrompt)
            .ToList();
}
