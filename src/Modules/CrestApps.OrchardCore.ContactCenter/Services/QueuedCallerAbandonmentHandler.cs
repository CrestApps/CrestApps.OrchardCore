using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Releases the queue work held for a caller who hung up while waiting for an agent.
/// </summary>
/// <remarks>
/// Ending the interaction is what the rest of the platform reads. The offer reconciliation that cancels
/// reservations and takes the item out of the queue only acts on an interaction that has ended, and the
/// abandonment figure on the call reports counts inbound calls that ended without being answered — so a caller
/// whose departure is never recorded is not merely stuck in a queue, they are missing from the one number that
/// would have shown people giving up.
/// </remarks>
public sealed class QueuedCallerAbandonmentHandler : IQueuedCallerAbandonmentHandler
{
    private readonly IInteractionManager _interactionManager;
    private readonly IProviderVoiceOfferSynchronizationService _offerSynchronizationService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedCallerAbandonmentHandler"/> class.
    /// </summary>
    public QueuedCallerAbandonmentHandler(
        IInteractionManager interactionManager,
        IProviderVoiceOfferSynchronizationService offerSynchronizationService,
        IClock clock,
        ILogger<QueuedCallerAbandonmentHandler> logger)
    {
        _interactionManager = interactionManager;
        _offerSynchronizationService = offerSynchronizationService;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task CallerAbandonedAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(activityItemId))
        {
            return;
        }

        var interaction = await _interactionManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        if (interaction is null)
        {
            return;
        }

        // Moved along its own lifecycle rather than assigned: the interaction refuses a transition it does not
        // admit, and one that has already settled needs no ending. Reconciliation below still runs either way --
        // it is idempotent, and the queue item is the part most likely to have been left behind.
        if (interaction.CanTransitionTo(InteractionStatus.Ended))
        {
            interaction.TransitionTo(InteractionStatus.Ended);
            interaction.EndedUtc ??= _clock.UtcNow;

            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        // The established path: cancel every reservation bound to the activity and take the item out of the
        // queue. Reusing it means a caller who abandons leaves the queue exactly as one whose call ended any
        // other way does.
        await _offerSynchronizationService.ReconcileEndedOfferAsync(interaction.ItemId, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The caller waiting on activity '{ActivityId}' hung up; their queue work has been released.",
                activityItemId.SanitizeLogValue());
        }
    }
}
