using CrestApps.Core.Support;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Outreach;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The default <see cref="IMessagingBroadcastService"/>: hands a queued broadcast to the outreach engine, which sends it
/// to each recipient in their own 1:1 thread (no cross-visibility), a page at a time, skipping recipients who opted out
/// and holding the sends in the channel's quiet hours. The broadcast keeps its recipient list and shows the run's
/// progress.
/// </summary>
public sealed class MessagingBroadcastService : IMessagingBroadcastService
{
    private readonly IMessagingBroadcastStore _broadcastStore;
    private readonly IOutreachEngine _outreachEngine;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingBroadcastService"/> class.
    /// </summary>
    /// <param name="broadcastStore">The broadcast store.</param>
    /// <param name="outreachEngine">The outreach engine that sends the broadcasts.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public MessagingBroadcastService(
        IMessagingBroadcastStore broadcastStore,
        IOutreachEngine outreachEngine,
        IClock clock,
        ILogger<MessagingBroadcastService> logger)
    {
        _broadcastStore = broadcastStore;
        _outreachEngine = outreachEngine;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = new List<MessagingBroadcast>();
        pending.AddRange(await _broadcastStore.GetByStatusAsync(MessagingBroadcastStatus.Running, cancellationToken));
        pending.AddRange(await _broadcastStore.GetByStatusAsync(MessagingBroadcastStatus.Queued, cancellationToken));

        foreach (var broadcast in pending)
        {
            await ProcessAsync(broadcast, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task ProcessAsync(MessagingBroadcast broadcast, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(broadcast);

        // Already handed to the engine, which reports its progress back on the broadcast.
        if (!string.IsNullOrEmpty(broadcast.OutreachRunId))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(broadcast.Channel) || string.IsNullOrWhiteSpace(broadcast.ServiceAddress) || string.IsNullOrWhiteSpace(broadcast.Body))
        {
            broadcast.Status = MessagingBroadcastStatus.Failed;
            broadcast.CompletedUtc = _clock.UtcNow;
            await _broadcastStore.UpdateAsync(broadcast, cancellationToken);

            _logger.LogWarning("Broadcast {BroadcastId} failed: a channel, a sending address and a body are required.", broadcast.ItemId.SanitizeLogValue());

            return;
        }

        // A broadcast that was half sent before the engine existed carries its counts over; its audience source leaves
        // out the recipients it already processed.
        var run = await _outreachEngine.StartAsync(new OutreachRun
        {
            Name = broadcast.Name,
            OwnerType = MessagingOutreachConstants.BroadcastOwnerType,
            OwnerId = broadcast.ItemId,
            AudienceSource = MessagingOutreachConstants.BroadcastAudienceSource,
            Sender = MessagingOutreachConstants.Sender,
            Channel = broadcast.Channel,

            // The engine asks the consent service about the broadcast's channel, so a recipient who opted out (a STOP,
            // a do-not-text flag on any record holding the number) is skipped rather than sent and refused.
            ConsentChannel = broadcast.Channel,
            ConsentContactKind = ConsentContactKinds.Contact,
            SenderAddress = broadcast.ServiceAddress,
            ActorId = broadcast.OwnerAgentId,
            Template = new OutreachTemplate { Body = broadcast.Body },
            Sent = broadcast.SentCount,
            Failed = broadcast.FailedCount,
            Skipped = broadcast.SkippedCount,
        }, cancellationToken);

        broadcast.OutreachRunId = run.ItemId;
        broadcast.Status = MessagingBroadcastStatus.Running;
        await _broadcastStore.UpdateAsync(broadcast, cancellationToken);
    }
}
