using CrestApps.Core;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Locking;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Services;

/// <summary>
/// Starts an automated SMS conversation for a text to a number whose text entry point routes to an AI agent, so the AI
/// answers the customer's first message the way it answers the replies to a conversation it started itself.
/// </summary>
public sealed class SmsEntryPointAIConversationStarter : IMessagingAIConversationStarter
{
    /// <summary>
    /// How long after an AI conversation ends a text from the same customer still goes to people rather than starting
    /// another one, so a "thanks" after the goodbye does not open a new conversation.
    /// </summary>
    public static readonly TimeSpan RestartQuietPeriod = TimeSpan.FromHours(1);

    private readonly IMessagingInboundRoutingResolver _routingResolver;
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IMessagingConversationStore _conversationStore;
    private readonly IMessagingContactResolver _contactResolver;
    private readonly IOmnichannelActivityStore _activityStore;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly IAIProfileManager _profileManager;
    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly IContentManager _contentManager;
    private readonly ILocalLock _localLock;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    // Both inbound handlers ask in the same scope; the answer for an address pair is kept so the second asks nothing.
    private readonly Dictionary<string, bool> _answers = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsEntryPointAIConversationStarter"/> class.
    /// </summary>
    /// <param name="routingResolver">Finds the entry point that answers the number.</param>
    /// <param name="channelResolver">Normalizes addresses the way the messaging workspace stores them.</param>
    /// <param name="conversationStore">Finds a human conversation already open with the customer.</param>
    /// <param name="contactResolver">Finds the contact who owns the customer's number.</param>
    /// <param name="activityStore">Finds the customer's automated conversations on the number.</param>
    /// <param name="activityManager">Creates the automated activity.</param>
    /// <param name="subjectFlowSettingsService">Finds the inbound SMS subject configured for the number.</param>
    /// <param name="profileManager">Finds the AI profile that answers.</param>
    /// <param name="chatSessionManager">Stores the conversation's AI chat session.</param>
    /// <param name="contentManager">Reads the contact and creates the subject.</param>
    /// <param name="localLock">Serializes starts for one customer on one number.</param>
    /// <param name="session">Commits the new conversation before the lock is released.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public SmsEntryPointAIConversationStarter(
        IMessagingInboundRoutingResolver routingResolver,
        IMessagingChannelResolver channelResolver,
        IMessagingConversationStore conversationStore,
        IMessagingContactResolver contactResolver,
        IOmnichannelActivityStore activityStore,
        IOmnichannelActivityManager activityManager,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IAIProfileManager profileManager,
        IAIChatSessionManager chatSessionManager,
        IContentManager contentManager,
        ILocalLock localLock,
        ISession session,
        IClock clock,
        ILogger<SmsEntryPointAIConversationStarter> logger)
    {
        _routingResolver = routingResolver;
        _channelResolver = channelResolver;
        _conversationStore = conversationStore;
        _contactResolver = contactResolver;
        _activityStore = activityStore;
        _activityManager = activityManager;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _profileManager = profileManager;
        _chatSessionManager = chatSessionManager;
        _contentManager = contentManager;
        _localLock = localLock;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> TryStartAsync(OmnichannelMessage message, OmnichannelChannelEndpoint address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (address is null ||
            !message.IsInbound ||
            !string.Equals(message.Channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(message.CustomerAddress))
        {
            return false;
        }

        var key = $"{address.ItemId}:{message.CustomerAddress}";

        if (_answers.TryGetValue(key, out var answer))
        {
            return answer;
        }

        answer = await StartAsync(message, address, key, cancellationToken);
        _answers[key] = answer;

        return answer;
    }

    private async Task<bool> StartAsync(OmnichannelMessage message, OmnichannelChannelEndpoint address, string key, CancellationToken cancellationToken)
    {
        var routing = await _routingResolver.ResolveAsync(address, OmnichannelConstants.Channels.Sms, cancellationToken);

        if (string.IsNullOrEmpty(routing?.AIProfileId))
        {
            return false;
        }

        // Two texts in the same second from a new customer are two deliveries handled side by side; only one of them
        // may start the conversation. The conversation is committed before the lock is released, so the other finds it.
        var (locker, locked) = await _localLock.TryAcquireLockAsync(
            $"SMS_AI_CONVERSATION_START_{key}",
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(1));

        if (!locked)
        {
            // Failing lets the provider's delivery be retried rather than handing the text to people by accident.
            throw new InvalidOperationException("The AI conversation for the inbound text could not be started because its lock was not acquired. The delivery will be retried.");
        }

        await using (locker)
        {
            var latest = await _activityStore.GetAsync(
                OmnichannelConstants.Channels.Sms,
                address.GetKnownIds(),
                message.CustomerAddress,
                ActivityInteractionType.Automated,
                cancellationToken);

            if (latest is not null && !latest.Status.IsTerminal())
            {
                return true;
            }

            if (latest is not null && _clock.UtcNow - (latest.CompletedUtc ?? latest.CreatedUtc) < RestartQuietPeriod)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "A text arrived on number '{AddressId}' less than {QuietMinutes} minutes after the customer's last AI conversation {ActivityId} ended, so it is left to people rather than starting another.",
                        address.ItemId.SanitizeLogValue(),
                        RestartQuietPeriod.TotalMinutes,
                        latest.ItemId.SanitizeLogValue());
                }

                return false;
            }

            if (await HasOpenHumanConversationAsync(message, address, cancellationToken))
            {
                return false;
            }

            var profile = await _profileManager.FindByIdAsync(routing.AIProfileId, cancellationToken);

            if (profile is null || profile.Type != AIProfileType.Chat)
            {
                _logger.LogWarning(
                    "Entry point '{EntryPointId}' routes texts to the AI profile '{ProfileId}', which is missing or not a chat profile; the text is left to people.",
                    routing.EntryPointId.SanitizeLogValue(),
                    routing.AIProfileId.SanitizeLogValue());

                return false;
            }

            var activity = await CreateConversationAsync(message, address, profile, cancellationToken);

            // Committed now, while the lock is held: the other delivery of this customer's texts, and the other handler
            // of this one, must find the conversation rather than start a second.
            await _session.SaveChangesAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Entry point '{EntryPointId}' started AI conversation {ActivityId} with profile '{ProfileId}' for a text to number '{AddressId}'.",
                    routing.EntryPointId.SanitizeLogValue(),
                    activity.ItemId.SanitizeLogValue(),
                    profile.ItemId.SanitizeLogValue(),
                    address.ItemId.SanitizeLogValue());
            }

            return true;
        }
    }

    // A person already talking with the customer on this number keeps the conversation: an AI taking over a thread a
    // person is working, or one marked spam, would answer for them.
    private async Task<bool> HasOpenHumanConversationAsync(OmnichannelMessage message, OmnichannelChannelEndpoint address, CancellationToken cancellationToken)
    {
        var channel = _channelResolver.Get(OmnichannelConstants.Channels.Sms);

        if (channel is null)
        {
            return false;
        }

        var conversation = await _conversationStore.FindByAddressesAsync(
            channel.Name,
            channel.NormalizeAddress(message.ServiceAddress),
            channel.NormalizeAddress(message.CustomerAddress),
            cancellationToken);

        return conversation is not null && conversation.Status != ConversationStatus.Closed;
    }

    private async Task<OmnichannelActivity> CreateConversationAsync(
        OmnichannelMessage message,
        OmnichannelChannelEndpoint address,
        AIProfile profile,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        // The inbound SMS subject set up for the number supplies the subject, campaign, dispositions and hand-off, as
        // the subject set up for a number does for the calls an AI voice agent answers. Without one the AI still
        // answers; it just has no dispositions to choose from and no one to hand the customer to.
        var flow = (await _subjectFlowSettingsService.GetConfiguredFlowSettingsAsync(cancellationToken))
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase) &&
                address.IsKnownAs(candidate.ChannelEndpointId));

        var chatSession = new AIChatSession
        {
            SessionId = UniqueId.GenerateId(),
            ProfileId = profile.ItemId,
            CreatedUtc = now,
            LastActivityUtc = now,
            Title = "Automated SMS conversation",
        };

        await _chatSessionManager.SaveAsync(chatSession, cancellationToken);

        var activity = await _activityManager.NewAsync(cancellationToken: cancellationToken);
        activity.Kind = ActivityKind.Sms;
        activity.Source = ActivitySources.Inbound;
        activity.Channel = OmnichannelConstants.Channels.Sms;
        activity.ChannelEndpointId = address.ItemId;
        activity.InteractionType = ActivityInteractionType.Automated;
        activity.AIProfileId = profile.ItemId;
        activity.AISessionId = chatSession.SessionId;
        activity.PreferredDestination = message.CustomerAddress;
        activity.CampaignId = flow?.CampaignId;
        activity.SubjectContentType = flow?.SubjectContentType;
        activity.AllowAIToUpdateContact = flow?.AllowAIToUpdateContact ?? false;
        activity.AllowAIToUpdateSubject = flow?.AllowAIToUpdateSubject ?? false;
        activity.AssignmentStatus = ActivityAssignmentStatus.Available;
        activity.CreatedUtc = now;

        // The customer wrote first, so the conversation is already waiting on the AI's answer to them. The no-response
        // timeout of the number's subject, when it has one, runs from here as it does after an opening message.
        activity.Status = ActivityStatus.AwaitingCustomerAnswer;
        activity.ScheduledUtc = flow is not null && OmnichannelAutomationHelper.HasNoResponseTimeout(flow)
            ? OmnichannelAutomationHelper.ResolveNoResponseDeadline(flow, now)
            : now;

        if (!string.IsNullOrEmpty(activity.SubjectContentType))
        {
            activity.Subject = await _contentManager.NewAsync(activity.SubjectContentType);
        }

        activity.ContactResolutionStatus = ContactResolutionStatus.Unresolved;

        var contactItemId = await _contactResolver.ResolveContactContentItemIdAsync(OmnichannelConstants.Channels.Sms, message.CustomerAddress, cancellationToken);

        if (!string.IsNullOrEmpty(contactItemId))
        {
            var contact = await _contentManager.GetAsync(contactItemId, VersionOptions.Latest);

            if (contact is not null)
            {
                activity.ContactContentItemId = contact.ContentItemId;
                activity.ContactContentType = contact.ContentType;
                activity.ContactResolutionStatus = ContactResolutionStatus.Resolved;
                activity.ContactResolvedUtc = now;
            }
        }

        await _activityManager.CreateAsync(activity, cancellationToken);

        return activity;
    }
}
