using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Outreach;

/// <summary>
/// Sends outreach messages on the messaging channels through the two-way send path, so each recipient gets their own
/// 1:1 thread (no one sees the others) owned by the run's actor. The run's <see cref="OutreachRun.Channel"/> names the
/// messaging channel, and its capabilities say whether quiet hours apply.
/// </summary>
public sealed class MessagingOutreachChannelSender : IOutreachChannelSender
{
    private readonly IMessagingConversationService _conversationService;
    private readonly IMessagingChannelResolver _channelResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingOutreachChannelSender"/> class.
    /// </summary>
    /// <param name="conversationService">The messaging conversation service.</param>
    /// <param name="channelResolver">The messaging channel resolver.</param>
    public MessagingOutreachChannelSender(IMessagingConversationService conversationService, IMessagingChannelResolver channelResolver)
    {
        _conversationService = conversationService;
        _channelResolver = channelResolver;
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
        }, cancellationToken);

        // A message held back by the address's sending limits, or waiting for a retry, is queued and the outbox sends it
        // later, so it is not a failure.
        return result.Succeeded || string.Equals(result.Message?.DeliveryStatus, MessageDeliveryStatus.Queued.ToString(), StringComparison.Ordinal)
            ? OutreachSendResult.Success()
            : OutreachSendResult.Failure(result.Error);
    }
}
