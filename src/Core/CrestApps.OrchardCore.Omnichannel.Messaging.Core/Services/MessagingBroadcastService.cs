using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The default <see cref="IMessagingBroadcastService"/>: works a broadcast's recipient list one message at a time
/// through the two-way send path, so each recipient gets an individual 1:1 thread (no cross-visibility) and a
/// resumed run never re-sends a recipient it already processed.
/// </summary>
public sealed class MessagingBroadcastService : IMessagingBroadcastService
{
    private readonly IMessagingBroadcastStore _broadcastStore;
    private readonly IMessagingConversationService _conversationService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingBroadcastService"/> class.
    /// </summary>
    public MessagingBroadcastService(
        IMessagingBroadcastStore broadcastStore,
        IMessagingConversationService conversationService,
        IClock clock,
        ILogger<MessagingBroadcastService> logger)
    {
        _broadcastStore = broadcastStore;
        _conversationService = conversationService;
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

        if (string.IsNullOrWhiteSpace(broadcast.Channel) || string.IsNullOrWhiteSpace(broadcast.ServiceAddress) || string.IsNullOrWhiteSpace(broadcast.Body))
        {
            broadcast.Status = MessagingBroadcastStatus.Failed;
            broadcast.CompletedUtc = _clock.UtcNow;
            await _broadcastStore.UpdateAsync(broadcast, cancellationToken);

            _logger.LogWarning("Broadcast {BroadcastId} failed: a channel, a sending address and a body are required.", broadcast.ItemId.SanitizeLogValue());

            return;
        }

        broadcast.Status = MessagingBroadcastStatus.Running;
        await _broadcastStore.UpdateAsync(broadcast, cancellationToken);

        var processed = new HashSet<string>(broadcast.ProcessedRecipients, StringComparer.OrdinalIgnoreCase);

        foreach (var recipient in broadcast.Recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(recipient) || !processed.Add(recipient))
            {
                continue;
            }

            MessagingSendResult result;

            try
            {
                result = await _conversationService.SendDirectAsync(broadcast.Channel, broadcast.ServiceAddress, recipient, broadcast.Body, broadcast.OwnerAgentId, cancellationToken);
            }
            catch (Exception ex)
            {
                result = MessagingSendResult.Failed(ex.Message);

                _logger.LogError(ex, "Broadcast {BroadcastId} failed to send to a recipient.", broadcast.ItemId.SanitizeLogValue());
            }

            if (result.Succeeded)
            {
                broadcast.SentCount++;
            }
            else
            {
                broadcast.FailedCount++;
            }

            broadcast.ProcessedRecipients.Add(recipient);

            // Persist progress after each recipient so a restart resumes without re-sending processed numbers.
            await _broadcastStore.UpdateAsync(broadcast, cancellationToken);
        }

        broadcast.Status = MessagingBroadcastStatus.Completed;
        broadcast.CompletedUtc = _clock.UtcNow;
        await _broadcastStore.UpdateAsync(broadcast, cancellationToken);
    }
}
