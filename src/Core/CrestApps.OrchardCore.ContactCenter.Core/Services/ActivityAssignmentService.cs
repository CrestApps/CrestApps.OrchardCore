using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IActivityAssignmentService"/>. It pairs the
/// highest-priority waiting item with the agent who has been available the longest (round robin by idle time).
/// </summary>
public sealed class ActivityAssignmentService : IActivityAssignmentService
{
    private readonly IQueueItemManager _queueItemManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IActivityReservationService _reservationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityAssignmentService"/> class.
    /// </summary>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="reservationService">The reservation service.</param>
    public ActivityAssignmentService(
        IQueueItemManager queueItemManager,
        IAgentProfileManager agentManager,
        IActivityQueueManager queueManager,
        IActivityReservationService reservationService)
    {
        _queueItemManager = queueItemManager;
        _agentManager = agentManager;
        _queueManager = queueManager;
        _reservationService = reservationService;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> AssignNextAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        var waiting = await _queueItemManager.ListWaitingAsync(queueId, cancellationToken);
        var topItem = waiting.FirstOrDefault();

        if (topItem is null)
        {
            return null;
        }

        var agents = await _agentManager.ListAvailableForQueueAsync(queueId, cancellationToken);
        var agent = agents.OrderBy(a => a.PresenceChangedUtc ?? DateTime.MaxValue).FirstOrDefault();

        if (agent is null)
        {
            return null;
        }

        var queue = await _queueManager.FindByIdAsync(queueId, cancellationToken);
        var timeout = queue?.ReservationTimeoutSeconds ?? 30;

        return await _reservationService.ReserveAsync(topItem, agent, timeout, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> AssignQueueAsync(string queueId, CancellationToken cancellationToken = default)
    {
        var count = 0;

        while (await AssignNextAsync(queueId, cancellationToken) is not null)
        {
            count++;
        }

        return count;
    }
}
