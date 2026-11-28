using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Telemetry;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Outreach;

/// <summary>
/// Sends outreach messages on the messaging channels through the two-way send path, each recipient on their own (no one
/// sees the others), as the run's actor. The messages are bulk mail, so they open no conversation: one joins a
/// conversation the recipient already has, and a conversation opens when the recipient replies. The run's
/// <see cref="OutreachRun.Channel"/> names the messaging channel, and its capabilities say whether quiet hours apply.
/// </summary>
public sealed class MessagingOutreachChannelSender : IOutreachChannelSender
{
    private readonly IMessagingConversationService _conversationService;
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IMessagingBroadcastStore _broadcastStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingOutreachChannelSender"/> class.
    /// </summary>
    /// <param name="conversationService">The messaging conversation service.</param>
    /// <param name="channelResolver">The messaging channel resolver.</param>
    /// <param name="broadcastStore">The store of the broadcasts whose held-back messages are counted as queued.</param>
    public MessagingOutreachChannelSender(
        IMessagingConversationService conversationService,
        IMessagingChannelResolver channelResolver,
        IMessagingBroadcastStore broadcastStore)
    {
        _conversationService = conversationService;
        _channelResolver = channelResolver;
        _broadcastStore = broadcastStore;
    }

    /// <inheritdoc/>
    public string Name
        => MessagingOutreachConstants.Sender;

    /// <inheritdoc/>
    public bool ObservesQuietHours(OutreachRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return !string.IsNullOrEmpty(run.Channel) && _channelResolver.Get(run.Channel)?.Capabilities.ObservesQuietHours == true;
    }

    /// <inheritdoc/>
    public async Task<OutreachSendResult> SendAsync(OutreachRun run, OutreachRecipient recipient, OutreachContent content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(content);

        var broadcastId = string.Equals(run.OwnerType, MessagingOutreachConstants.BroadcastOwnerType, StringComparison.Ordinal)
            ? run.OwnerId
            : null;

        // Sent as bulk mail, so the sending address's limits apply and an email carries its unsubscribe link.
        var result = await _conversationService.SendDirectAsync(new MessagingDirectSendRequest
        {
            Channel = run.Channel,
            ServiceAddress = run.SenderAddress,
            ContactAddress = recipient.Address,
            Subject = content.Subject,
            Body = content.Body,
            ActingAgentId = run.ActorId,
            Purpose = MessagingOutboundPurpose.Broadcast,
            BroadcastId = broadcastId,
        }, cancellationToken);

        // A message held back by the address's sending limits, or waiting for a retry, is queued and the outbox sends it
        // later, so it is not a failure. It is not sent yet either: the broadcast counts it as queued until it is.
        var queued = !result.Succeeded &&
            string.Equals(result.Message?.DeliveryStatus, MessageDeliveryStatus.Queued.ToString(), StringComparison.Ordinal);

        var accepted = result.Succeeded || queued;

        if (string.Equals(run.OwnerType, MessagingOutreachConstants.BroadcastOwnerType, StringComparison.Ordinal))
        {
            MessagingTelemetry.RecordBroadcastRecipient(_channelResolver.Get(run.Channel)?.Name, accepted);
        }

        if (queued && !string.IsNullOrEmpty(broadcastId))
        {
            await RecordQueuedAsync(broadcastId, cancellationToken);
        }

        return accepted
            ? OutreachSendResult.Success()
            : OutreachSendResult.Failure(result.Error);
    }

    // The engine commits after every recipient, which detaches what the session read before; the broadcast is read again
    // each time, so the count is added to the stored broadcast rather than to a stale copy.
    private async Task RecordQueuedAsync(string broadcastId, CancellationToken cancellationToken)
    {
        var broadcast = await _broadcastStore.FindByIdAsync(broadcastId, cancellationToken);

        if (broadcast is null)
        {
            return;
        }

        broadcast.RecordQueued();

        await _broadcastStore.UpdateAsync(broadcast, cancellationToken);
    }
}
