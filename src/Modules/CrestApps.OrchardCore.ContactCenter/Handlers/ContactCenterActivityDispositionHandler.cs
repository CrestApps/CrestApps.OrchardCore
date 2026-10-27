using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Releases an agent from wrap-up and offers the next queued voice activity after completing assigned work.
/// </summary>
public sealed class ContactCenterActivityDispositionHandler : IActivityDispositionHandler
{
    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IContactCenterWorkStateService _workStateService;
    private readonly IQueuedVoiceWorkOfferService _queuedVoiceWorkOfferService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterActivityDispositionHandler"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="presenceManager">The agent presence manager.</param>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="workStateService">The routing-owned work state service.</param>
    /// <param name="queuedVoiceWorkOfferService">The queued voice work offer service.</param>
    /// <param name="clock">The clock used to complete wrap-up timing.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterActivityDispositionHandler(
        IAgentProfileManager agentManager,
        IAgentPresenceManager presenceManager,
        IInteractionManager interactionManager,
        IContactCenterWorkStateService workStateService,
        IQueuedVoiceWorkOfferService queuedVoiceWorkOfferService,
        IClock clock,
        ILogger<ContactCenterActivityDispositionHandler> logger)
    {
        _agentManager = agentManager;
        _presenceManager = presenceManager;
        _interactionManager = interactionManager;
        _workStateService = workStateService;
        _queuedVoiceWorkOfferService = queuedVoiceWorkOfferService;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task DispositionedAsync(ActivityDispositionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var activity = request.Activity;

        if (activity is null || activity.Status != ActivityStatus.Completed)
        {
            return;
        }

        // The assigned agent is read from routing's own work state rather than the CRM projection, because a
        // disposition can be recorded in the same request that routing assigned the work in, before the
        // projection has reconciled the activity row.
        var workState = await _workStateService.GetAsync(activity.ItemId, cancellationToken);

        if (string.IsNullOrWhiteSpace(workState?.AssignedToId))
        {
            return;
        }

        var interaction = await _interactionManager.FindByActivityIdAsync(activity.ItemId, cancellationToken);

        if (interaction?.WrapUpStartedUtc is not null && interaction.WrapUpCompletedUtc is null)
        {
            interaction.WrapUpCompletedUtc = _clock.UtcNow;
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        var agent = await _agentManager.FindByUserIdAsync(workState.AssignedToId, cancellationToken);

        if (agent is null ||
            !string.IsNullOrWhiteSpace(agent.ActiveReservationId) ||
            agent.PresenceStatus is not AgentPresenceStatus.Busy and not AgentPresenceStatus.WrapUp)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipped Contact Center completion reconciliation for activity '{ActivityId}'. AgentId={AgentId}, Presence={PresenceStatus}, ActiveReservationId={ActiveReservationId}.",
                    activity.ItemId.SanitizeLogValue(),
                    agent?.ItemId.SanitizeLogValue(),
                    agent?.PresenceStatus,
                    agent?.ActiveReservationId.SanitizeLogValue());
            }

            return;
        }

        // A disposition can land on an activity the agent finished earlier (an outcome recorded after the call, a
        // supervisor's, the recovery's) while the agent is already on their next call. Busy then belongs to that call:
        // completing the work here would make the agent available mid-call and offer them queued work.
        var liveInteraction = await _interactionManager.FindActiveByAgentAsync(agent.ItemId, cancellationToken);

        if (liveInteraction is not null && liveInteraction.ActivityItemId != activity.ItemId)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipped Contact Center completion reconciliation for activity '{ActivityId}' because agent '{AgentId}' is on interaction '{InteractionId}' for another activity.",
                    activity.ItemId.SanitizeLogValue(),
                    agent.ItemId.SanitizeLogValue(),
                    liveInteraction.ItemId.SanitizeLogValue());
            }

            return;
        }

        agent = await _presenceManager.CompleteWorkAsync(agent.ItemId, cancellationToken);

        if (agent?.PresenceStatus == AgentPresenceStatus.Available)
        {
            var offered = await _queuedVoiceWorkOfferService.OfferForAgentAsync(agent.ItemId, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Completed wrap-up for agent '{AgentId}' after activity '{ActivityId}' was dispositioned and offered {OfferedCount} queued voice activities.",
                    agent.ItemId.SanitizeLogValue(),
                    activity.ItemId.SanitizeLogValue(),
                    offered);
            }
        }
    }
}
