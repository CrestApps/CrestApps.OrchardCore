using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Offers already-waiting inbound voice work to an agent who has just become reachable again.
/// </summary>
public sealed class QueuedVoiceWorkOfferService : IQueuedVoiceWorkOfferService
{
    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentWorkStateHealingService _agentWorkStateHealingService;
    private readonly IInboundVoiceService _inboundVoiceService;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IQueueItemStore _queueItemStore;
    private readonly IInteractionManager _interactionManager;
    private readonly IDialerProfileReader _dialerProfileReader;
    private readonly IAgentWorkSelector _workSelector;
    private readonly IDistributedLock _distributedLock;
    private readonly ContactCenterCoordinationOptions _coordinationOptions;
    private readonly ISession _session;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedVoiceWorkOfferService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="agentWorkStateHealingService">The agent work-state healer.</param>
    /// <param name="inboundVoiceService">The inbound voice service.</param>
    /// <param name="queueItemManager">The queue item manager used to find held direct-to-agent calls.</param>
    /// <param name="queueItemStore">The queue item store used to peek a campaign queue's head item.</param>
    /// <param name="interactionManager">The interaction manager used to resolve a held call's direct target.</param>
    /// <param name="dialerProfileReader">
    /// The dialer profile reader. It is used to classify a campaign queue's dialing mode so automated (paced)
    /// outbound work is left to the pacing engine instead of being offered here; without the Outbound Dialer
    /// feature it finds no profile, and the queue is offered normally.
    /// </param>
    /// <param name="distributedLock">The distributed lock used to debounce repeated sync requests for one agent.</param>
    /// <param name="coordinationOptions">The coordination options carrying the sync lease.</param>
    /// <param name="session">The YesSql session used to persist availability before querying routing indexes.</param>
    /// <param name="logger">The logger.</param>
    public QueuedVoiceWorkOfferService(
        IAgentProfileManager agentManager,
        IAgentWorkStateHealingService agentWorkStateHealingService,
        IInboundVoiceService inboundVoiceService,
        IQueueItemManager queueItemManager,
        IQueueItemStore queueItemStore,
        IInteractionManager interactionManager,
        IDialerProfileReader dialerProfileReader,
        IAgentWorkSelector workSelector,
        IDistributedLock distributedLock,
        IOptions<ContactCenterCoordinationOptions> coordinationOptions,
        ISession session,
        ILogger<QueuedVoiceWorkOfferService> logger)
    {
        _agentManager = agentManager;
        _agentWorkStateHealingService = agentWorkStateHealingService;
        _inboundVoiceService = inboundVoiceService;
        _queueItemManager = queueItemManager;
        _queueItemStore = queueItemStore;
        _interactionManager = interactionManager;
        _dialerProfileReader = dialerProfileReader;
        _workSelector = workSelector;
        _distributedLock = distributedLock;
        _coordinationOptions = coordinationOptions.Value;
        _session = session;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> OfferForAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        var agent = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        return await OfferForProfileAsync(agent, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> OfferForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var agent = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        return await OfferForProfileAsync(agent, cancellationToken);
    }

    private async Task<int> OfferForProfileAsync(AgentProfile agent, CancellationToken cancellationToken)
    {
        // The client asks for its queued work on connect, on reconnect, on every presence change and when an
        // offer completes, and several of those can land together. A short lease per agent collapses that burst
        // into one scan: a caller that cannot take the lease returns immediately, because another sync for the
        // same agent is either in flight or has just finished and its answer is still current.
        if (agent is not null)
        {
            var (locker, locked) = await _distributedLock.TryAcquireLockAsync(
                $"ContactCenterQueuedWorkSync:{agent.ItemId}",
                TimeSpan.Zero,
                _coordinationOptions.QueuedWorkSyncLease);

            if (!locked)
            {
                return 0;
            }

            await using (locker)
            {
                return await OfferForProfileCoreAsync(agent, cancellationToken);
            }
        }

        return await OfferForProfileCoreAsync(agent, cancellationToken);
    }

    private async Task<int> OfferForProfileCoreAsync(AgentProfile agent, CancellationToken cancellationToken)
    {
        // Queue membership is not required: a direct-to-agent (personal line) agent may belong to no queue yet
        // still have a call held for them. Only presence gates whether any waiting work can be offered.
        if (agent is null ||
            agent.PresenceStatus != AgentPresenceStatus.Available)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipped queued voice offering because agent was missing or unavailable. AgentId={AgentId}, Presence={PresenceStatus}.",
                    agent?.ItemId.SanitizeLogValue(),
                    agent?.PresenceStatus);
            }

            return 0;
        }

        await _agentWorkStateHealingService.HealForAvailabilityAsync(agent.ItemId, cancellationToken);
        agent = await _agentManager.FindByIdAsync(agent.ItemId, cancellationToken) ?? agent;

        if (!string.IsNullOrWhiteSpace(agent.ActiveReservationId))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipped queued voice offering for agent '{AgentId}' because reservation '{ReservationId}' is active.",
                    agent.ItemId.SanitizeLogValue(),
                    agent.ActiveReservationId.SanitizeLogValue());
            }

            return 0;
        }

        await _session.SaveChangesAsync(cancellationToken);

        // A held direct-to-agent (personal line) call takes precedence: it is a caller already waiting
        // specifically for this agent, and it does not depend on any queue membership. Offering one reserves
        // the agent, so when that succeeds we are done and must not also pull from queues.
        if (await OfferHeldDirectCallsAsync(agent, cancellationToken) > 0)
        {
            return 1;
        }

        var offered = 0;

        // The selector picks the queue holding the contact who most deserves to be answered across everything
        // this agent serves. Walking the agent's stored list in order meant a caller waiting twenty minutes on
        // the second queue sat behind one who had just arrived on the first.
        //
        // The loop re-selects after each offer because an agent may still be available (an offer can be declined
        // or find nobody), and the next-best queue may have changed. A queue this pass could not serve - a paced
        // campaign the dialer owns, or one whose head item this agent could not take - is excluded from the next
        // selection instead of ending the pass, so an inbound queue behind it is still reached. The pass ends as
        // soon as the agent is reserved, stops being available, or no queue has eligible work left.
        var excludedQueueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var maxPasses = Math.Max(1, agent.QueueIds.Count + agent.QueueMemberships.Count) + 1;

        for (var pass = 0; pass < maxPasses; pass++)
        {
            var queueId = await _workSelector.SelectNextForAgentAsync(agent, excludedQueueIds, cancellationToken);

            if (string.IsNullOrEmpty(queueId))
            {
                break;
            }

            // Outbound campaign work dialed by an automated (paced) mode - Power, Progressive, or Predictive -
            // is placed by the dialer pacing engine, which reserves the agent itself. Offering it here would
            // reserve the head item and immediately reject it (it has no interaction yet and is not a preview
            // offer), so the reservation would churn and starve the pacing engine that actually places the call.
            // The queue is left to the pacing engine and the selection moves on.
            if (await IsAutomatedPacedCampaignQueueAsync(queueId, cancellationToken))
            {
                excludedQueueIds.Add(queueId);

                continue;
            }

            var agentUserId = await _inboundVoiceService.OfferNextAsync(queueId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(agentUserId))
            {
                offered++;

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Offered the next queued voice activity from queue '{QueueId}' to agent '{AgentId}' for user '{UserId}'.",
                        queueId.SanitizeLogValue(),
                        agent.ItemId.SanitizeLogValue(),
                        agentUserId.SanitizeLogValue());
                }
            }

            agent = await _agentManager.FindByIdAsync(agent.ItemId, cancellationToken);

            if (agent is null ||
                agent.PresenceStatus != AgentPresenceStatus.Available ||
                !string.IsNullOrWhiteSpace(agent.ActiveReservationId))
            {
                break;
            }

            // Nothing was offered and the agent is still free, so the queue the selector chose had nothing this
            // agent could take (a skill or capacity rule, or a race with another node). Selecting again would
            // choose it again, so it is excluded and the next-best queue gets its turn.
            if (string.IsNullOrWhiteSpace(agentUserId))
            {
                excludedQueueIds.Add(queueId);
            }
        }

        return offered;
    }

    // Determines whether a queue is an outbound campaign queue whose head waiting item is dialed by an automated
    // (paced) mode. Only campaign queues can carry automated dialing, so an inbound queue short-circuits without a
    // lookup. Without the Outbound Dialer feature the reader finds no profile, so no automated pacing exists and
    // the queue is offered normally.
    private async Task<bool> IsAutomatedPacedCampaignQueueAsync(string queueId, CancellationToken cancellationToken)
    {
        if (!ContactCenterConstants.IsCampaignQueue(queueId))
        {
            return false;
        }

        var headItem = await _queueItemStore.FindNextWaitingAsync(queueId, cancellationToken);

        if (headItem is null || string.IsNullOrEmpty(headItem.DialerProfileId))
        {
            return false;
        }

        var profile = await _dialerProfileReader.FindByIdAsync(headItem.DialerProfileId, cancellationToken);

        return profile is not null && profile.Mode.IsAutomated();
    }

    // Offers a call that was held for this specific agent while they were unavailable. Held direct calls wait
    // under the synthetic direct-routing queue tagged with their target agent; this connects the longest-held
    // caller for the agent as soon as the agent becomes available. The agent can take one call at a time, so at
    // most one held call is offered here.
    private async Task<int> OfferHeldDirectCallsAsync(AgentProfile agent, CancellationToken cancellationToken)
    {
        var waiting = await _queueItemManager.GetWaitingAsync(ContactCenterConstants.DirectRouting.QueueId, cancellationToken);

        if (waiting.Count == 0)
        {
            return 0;
        }

        foreach (var item in waiting.OrderBy(queueItem => queueItem.EnqueuedUtc))
        {
            var interaction = await _interactionManager.FindByActivityIdAsync(item.ActivityItemId, cancellationToken);

            if (interaction is null ||
                !interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.DirectRouting.TargetAgentMetadataKey, out var target) ||
                !string.Equals(target?.ToString(), agent.ItemId, StringComparison.Ordinal))
            {
                continue;
            }

            var offeredUserId = await _inboundVoiceService.OfferToAgentAsync(
                item.ActivityItemId,
                ContactCenterConstants.DirectRouting.QueueId,
                agent.ItemId,
                ReadRingTimeoutSeconds(interaction),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(offeredUserId))
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Offered a held direct-to-agent call for activity '{ActivityItemId}' to agent '{AgentId}' after they became available.",
                        item.ActivityItemId.SanitizeLogValue(),
                        agent.ItemId.SanitizeLogValue());
                }

                return 1;
            }
        }

        return 0;
    }

    private static int? ReadRingTimeoutSeconds(Interaction interaction)
    {
        if (interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.DirectRouting.RingTimeoutMetadataKey, out var value) &&
            value is not null &&
            int.TryParse(value.ToString(), out var seconds) &&
            seconds > 0)
        {
            return seconds;
        }

        return null;
    }
}
