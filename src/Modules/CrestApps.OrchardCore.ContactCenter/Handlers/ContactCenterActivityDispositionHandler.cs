using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Releases an agent from wrap-up and offers the next queued voice activity after completing assigned work.
/// </summary>
public sealed class ContactCenterActivityDispositionHandler : IActivityDispositionHandler
{
    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IQueuedVoiceWorkOfferService _queuedVoiceWorkOfferService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterActivityDispositionHandler"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="presenceManager">The agent presence manager.</param>
    /// <param name="queuedVoiceWorkOfferServices">The optional queued voice work offer services.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterActivityDispositionHandler(
        IAgentProfileManager agentManager,
        IAgentPresenceManager presenceManager,
        IEnumerable<IQueuedVoiceWorkOfferService> queuedVoiceWorkOfferServices,
        ILogger<ContactCenterActivityDispositionHandler> logger)
    {
        _agentManager = agentManager;
        _presenceManager = presenceManager;
        _queuedVoiceWorkOfferService = queuedVoiceWorkOfferServices.FirstOrDefault();
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task DispositionedAsync(ActivityDispositionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var activity = request.Activity;

        if (activity is null ||
            activity.Status != ActivityStatus.Completed ||
            string.IsNullOrWhiteSpace(activity.AssignedToId))
        {
            return;
        }

        var agent = await _agentManager.FindByUserIdAsync(activity.AssignedToId, cancellationToken);

        if (agent is null ||
            !string.IsNullOrWhiteSpace(agent.ActiveReservationId) ||
            agent.PresenceStatus is not AgentPresenceStatus.Busy and not AgentPresenceStatus.WrapUp)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipped Contact Center completion reconciliation for activity '{ActivityId}'. AgentId={AgentId}, Presence={PresenceStatus}, ActiveReservationId={ActiveReservationId}.",
                    activity.ItemId,
                    agent?.ItemId,
                    agent?.PresenceStatus,
                    agent?.ActiveReservationId);
            }

            return;
        }

        agent = await _presenceManager.CompleteWorkAsync(agent.ItemId, cancellationToken);

        if (agent?.PresenceStatus == AgentPresenceStatus.Available &&
            _queuedVoiceWorkOfferService is not null)
        {
            var offered = await _queuedVoiceWorkOfferService.OfferForAgentAsync(agent.ItemId, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Completed wrap-up for agent '{AgentId}' after activity '{ActivityId}' was dispositioned and offered {OfferedCount} queued voice activities.",
                    agent.ItemId,
                    activity.ItemId,
                    offered);
            }
        }
    }
}
