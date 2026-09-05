using CrestApps.Core.Support;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

/// <summary>
/// The default <see cref="ISmsBroadcastService"/>: works a broadcast's recipient list one message at a time
/// through the two-way send path, so each recipient gets an individual 1:1 thread (no cross-visibility) and a
/// resumed run never re-sends a recipient it already processed.
/// </summary>
public sealed class SmsBroadcastService : ISmsBroadcastService
{
    private readonly ISmsBroadcastStore _broadcastStore;
    private readonly ISmsConversationService _conversationService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsBroadcastService"/> class.
    /// </summary>
    public SmsBroadcastService(
        ISmsBroadcastStore broadcastStore,
        ISmsConversationService conversationService,
        IClock clock,
        ILogger<SmsBroadcastService> logger)
    {
        _broadcastStore = broadcastStore;
        _conversationService = conversationService;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = new List<SmsBroadcast>();
        pending.AddRange(await _broadcastStore.GetByStatusAsync(SmsBroadcastStatus.Running, cancellationToken));
        pending.AddRange(await _broadcastStore.GetByStatusAsync(SmsBroadcastStatus.Queued, cancellationToken));

        foreach (var broadcast in pending)
        {
            await ProcessAsync(broadcast, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task ProcessAsync(SmsBroadcast broadcast, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(broadcast);

        if (string.IsNullOrWhiteSpace(broadcast.FromNumber) || string.IsNullOrWhiteSpace(broadcast.Body))
        {
            broadcast.Status = SmsBroadcastStatus.Failed;
            broadcast.CompletedUtc = _clock.UtcNow;
            await _broadcastStore.UpdateAsync(broadcast, cancellationToken);

            _logger.LogWarning("Broadcast {BroadcastId} failed: a sending number and body are required.", broadcast.ItemId.SanitizeLogValue());

            return;
        }

        broadcast.Status = SmsBroadcastStatus.Running;
        await _broadcastStore.UpdateAsync(broadcast, cancellationToken);

        var processed = new HashSet<string>(broadcast.ProcessedRecipients, StringComparer.OrdinalIgnoreCase);

        foreach (var recipient in broadcast.Recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(recipient) || !processed.Add(recipient))
            {
                continue;
            }

            SmsSendResult result;

            try
            {
                result = await _conversationService.SendDirectAsync(broadcast.FromNumber, recipient, broadcast.Body, broadcast.OwnerAgentId, cancellationToken);
            }
            catch (Exception ex)
            {
                result = SmsSendResult.Failed(ex.Message);

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

        broadcast.Status = SmsBroadcastStatus.Completed;
        broadcast.CompletedUtc = _clock.UtcNow;
        await _broadcastStore.UpdateAsync(broadcast, cancellationToken);
    }
}
