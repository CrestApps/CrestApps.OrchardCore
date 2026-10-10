using CrestApps.Core;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Locking;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Automation;

/// <summary>
/// Starts an AI conversation for a customer's message to an address whose entry point routes to an AI agent, on any
/// automated channel. It is asked by both the automated handler and the messaging workspace, so it is scoped and
/// remembers its answer for an address pair: whichever handler sees the message first starts the conversation once, and
/// the other reads the same answer.
/// </summary>
/// <remarks>
/// A conversation is not started, and the message is left to people, when the entry point is closed, a person already
/// has an open conversation with the customer, the customer's last AI conversation ended less than an hour ago, the
/// profile is missing, or the message was written by a machine.
/// </remarks>
public sealed class AutomatedEntryPointConversationStarter : IMessagingAIConversationStarter
{
    /// <summary>
    /// How long after an AI conversation ends a new message from the same customer goes to people rather than starting
    /// another AI conversation, so a customer who just finished with the AI is not greeted by it again.
    /// </summary>
    public static readonly TimeSpan RestartQuietPeriod = TimeSpan.FromHours(1);

    private readonly IEnumerable<IAutomatedMessagingChannel> _channels;
    private readonly IMessagingInboundRoutingResolver _routingResolver;
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
    /// Initializes a new instance of the <see cref="AutomatedEntryPointConversationStarter"/> class.
    /// </summary>
    /// <param name="channels">The automated channels.</param>
    /// <param name="routingResolver">The resolver of an address's entry point routing.</param>
    /// <param name="conversationStore">The workspace's conversation store.</param>
    /// <param name="contactResolver">The resolver of the contact behind an address.</param>
    /// <param name="activityStore">The activity store.</param>
    /// <param name="activityManager">The activity manager the conversation's activity is created through.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="chatSessionManager">The AI chat session manager.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="localLock">The lock that keeps one start per customer.</param>
    /// <param name="session">The session the conversation is committed with.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AutomatedEntryPointConversationStarter(
        IEnumerable<IAutomatedMessagingChannel> channels,
        IMessagingInboundRoutingResolver routingResolver,
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
        ILogger<AutomatedEntryPointConversationStarter> logger)
    {
        _channels = channels;
        _routingResolver = routingResolver;
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

        var channel = _channels.FirstOrDefault(candidate => string.Equals(candidate.Channel, message.Channel, StringComparison.OrdinalIgnoreCase));

        if (channel is null ||
            address is null ||
            !message.IsInbound ||
            string.IsNullOrWhiteSpace(message.CustomerAddress) ||
            channel.IsAutomaticMessage(message))
        {
            return false;
        }

        var customerAddress = channel.NormalizeAddress(message.CustomerAddress);
        var key = $"{address.ItemId}:{customerAddress}";

        if (_answers.TryGetValue(key, out var answer))
        {
            return answer;
        }

        answer = await StartAsync(channel, message, address, customerAddress, key, cancellationToken);
        _answers[key] = answer;

        return answer;
    }

    private async Task<bool> StartAsync(IAutomatedMessagingChannel channel, OmnichannelMessage message, OmnichannelChannelEndpoint address, string customerAddress, string key, CancellationToken cancellationToken)
    {
        var routing = await _routingResolver.ResolveAsync(address, channel.Channel, cancellationToken);

        if (string.IsNullOrEmpty(routing?.AIProfileId))
        {
            return false;
        }

        // Two messages in the same second from a new customer are handled side by side; only one may start the
        // conversation. It is committed before the lock is released, so the other finds it.
        var (locker, locked) = await _localLock.TryAcquireLockAsync($"AUTOMATED_CONVERSATION_START_{key}", TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));

        if (!locked)
        {
            // Failing lets the delivery be retried rather than handing the message to people by accident.
            throw new InvalidOperationException("The AI conversation for the inbound message could not be started because its lock was not acquired. The delivery will be retried.");
        }

        await using (locker)
        {
            var latest = await _activityStore.GetAsync(channel.Channel, address.GetKnownIds(), customerAddress, ActivityInteractionType.Automated, cancellationToken);

            if (latest is not null && !latest.Status.IsTerminal())
            {
                return true;
            }

            if (latest is not null && _clock.UtcNow - (latest.CompletedUtc ?? latest.CreatedUtc) < RestartQuietPeriod)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("A {Channel} message arrived on address '{AddressId}' less than {QuietMinutes} minutes after the customer's last AI conversation {ActivityId} ended, so it is left to people.", channel.Channel, address.ItemId.SanitizeLogValue(), RestartQuietPeriod.TotalMinutes, latest.ItemId.SanitizeLogValue());
                }

                return false;
            }

            // A person already talking with the customer on this address keeps the conversation.
            var conversation = await _conversationStore.FindByAddressesAsync(channel.Channel, channel.NormalizeAddress(address.Value), customerAddress, cancellationToken);

            if (conversation is not null && conversation.Status != ConversationStatus.Closed)
            {
                return false;
            }

            var profile = await _profileManager.FindByIdAsync(routing.AIProfileId, cancellationToken);

            if (profile is null || profile.Type != AIProfileType.Chat)
            {
                _logger.LogWarning("Entry point '{EntryPointId}' routes {Channel} messages to the AI profile '{ProfileId}', which is missing or not a chat profile; the message is left to people.", routing.EntryPointId.SanitizeLogValue(), channel.Channel, routing.AIProfileId.SanitizeLogValue());

                return false;
            }

            var activity = await CreateConversationAsync(channel, customerAddress, address, profile, cancellationToken);

            // Committed now, while the lock is held, so the other handler of this message and the next message find it.
            await _session.SaveChangesAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Entry point '{EntryPointId}' started AI conversation {ActivityId} with profile '{ProfileId}' for a {Channel} message to address '{AddressId}'.", routing.EntryPointId.SanitizeLogValue(), activity.ItemId.SanitizeLogValue(), profile.ItemId.SanitizeLogValue(), channel.Channel, address.ItemId.SanitizeLogValue());
            }

            return true;
        }
    }

    private async Task<OmnichannelActivity> CreateConversationAsync(IAutomatedMessagingChannel channel, string customerAddress, OmnichannelChannelEndpoint address, AIProfile profile, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        // The inbound subject set up for the address supplies the subject, campaign, dispositions and handoff. Without one
        // the AI still answers; it just has no dispositions to choose from and no one to hand the customer to.
        var flow = (await _subjectFlowSettingsService.GetConfiguredFlowSettingsAsync(cancellationToken))
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Channel, channel.Channel, StringComparison.OrdinalIgnoreCase) &&
                address.IsKnownAs(candidate.ChannelEndpointId));

        var chatSession = new AIChatSession
        {
            SessionId = UniqueId.GenerateId(),
            ProfileId = profile.ItemId,
            CreatedUtc = now,
            LastActivityUtc = now,
            Title = $"Automated {channel.ConversationNoun}",
        };

        await _chatSessionManager.SaveAsync(chatSession, cancellationToken);

        var activity = await _activityManager.NewAsync(cancellationToken: cancellationToken);
        activity.Kind = channel.ActivityKind;
        activity.Source = ActivitySources.Inbound;
        activity.Channel = channel.Channel;
        activity.ChannelEndpointId = address.ItemId;
        activity.InteractionType = ActivityInteractionType.Automated;
        activity.AIProfileId = profile.ItemId;
        activity.AISessionId = chatSession.SessionId;
        activity.PreferredDestination = customerAddress;
        activity.CampaignId = flow?.CampaignId;
        activity.SubjectContentType = flow?.SubjectContentType;
        activity.AllowAIToUpdateContact = flow?.AllowAIToUpdateContact ?? false;
        activity.AllowAIToUpdateSubject = flow?.AllowAIToUpdateSubject ?? false;
        activity.AssignmentStatus = ActivityAssignmentStatus.Available;
        activity.CreatedUtc = now;

        // The customer wrote first, so the conversation is already waiting on the AI's answer to them.
        activity.Status = ActivityStatus.AwaitingCustomerAnswer;
        activity.ScheduledUtc = flow is not null && OmnichannelAutomationHelper.HasNoResponseTimeout(flow)
            ? OmnichannelAutomationHelper.ResolveNoResponseDeadline(flow, now)
            : now;

        if (!string.IsNullOrEmpty(activity.SubjectContentType))
        {
            activity.Subject = await _contentManager.NewAsync(activity.SubjectContentType);
        }

        activity.ContactResolutionStatus = ContactResolutionStatus.Unresolved;

        var contactItemId = await _contactResolver.ResolveContactContentItemIdAsync(channel.Channel, customerAddress, cancellationToken);

        if (!string.IsNullOrEmpty(contactItemId) &&
            await _contentManager.GetAsync(contactItemId, VersionOptions.Latest) is { } contact)
        {
            activity.ContactContentItemId = contact.ContentItemId;
            activity.ContactContentType = contact.ContentType;
            activity.ContactResolutionStatus = ContactResolutionStatus.Resolved;
            activity.ContactResolvedUtc = now;
        }

        await _activityManager.CreateAsync(activity, cancellationToken);

        return activity;
    }
}
