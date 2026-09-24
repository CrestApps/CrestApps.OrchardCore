using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IAgentStateTransitionService"/>.
/// </summary>
public sealed class AgentStateTransitionService : IAgentStateTransitionService
{
    private readonly IContactCenterAuditRecorder _auditRecorder;
    private readonly IAgentStateReasonCodeManager _reasonCodeManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateTransitionService"/> class.
    /// </summary>
    /// <param name="auditRecorder">The recorder every transition is written through.</param>
    /// <param name="reasonCodeManager">The configured reason codes a given reason is resolved against.</param>
    /// <param name="clock">The clock used to date transitions that happen now.</param>
    /// <param name="logger">The logger.</param>
    public AgentStateTransitionService(
        IContactCenterAuditRecorder auditRecorder,
        IAgentStateReasonCodeManager reasonCodeManager,
        IClock clock,
        ILogger<AgentStateTransitionService> logger)
    {
        _auditRecorder = auditRecorder;
        _reasonCodeManager = reasonCodeManager;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AgentStateChangedEventData> TransitionAsync(
        AgentProfile profile,
        AgentPresenceStatus state,
        AgentStateChangeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        context ??= new AgentStateChangeContext();

        var previousState = profile.PresenceStatus;
        var changedUtc = ResolveChangedUtc(context.ChangedUtc, profile.PresenceChangedUtc);

        profile.PresenceStatus = state;
        profile.PresenceChangedUtc = changedUtc;

        // Once nothing is pending there is no request left to have been made, so the stamp cannot outlive it and
        // mistake a later captured return state for something the agent asked for.
        if (!profile.RequestedPresenceStatus.HasValue)
        {
            profile.PresenceRequestedUtc = null;
        }

        ClearStaleReasonOnReady(profile, state, context);

        if (previousState == state && !context.RecordWhenUnchanged)
        {
            return null;
        }

        if (string.IsNullOrEmpty(profile.ItemId))
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Could not record the Contact Center agent state change from '{PreviousState}' to '{CurrentState}' for user '{UserId}' because the agent profile has no identifier.",
                    previousState,
                    state,
                    profile.UserId.SanitizeLogValue());
            }

            return null;
        }

        var change = new AgentStateChangedEventData
        {
            AgentId = profile.ItemId,
            UserId = profile.UserId,
            PreviousState = previousState,
            CurrentState = state,
            RequestedState = profile.RequestedPresenceStatus,
            ReasonCodeId = context.ReasonCodeId,
            ReasonName = context.ReasonName,
            Source = context.Source,
            InteractionId = context.InteractionId,
            ReservationId = context.ReservationId,
            ReleaseReason = context.ReleaseReason,
            AgentSessionId = context.AgentSessionId,
            QueueIds = profile.QueueIds?.ToList() ?? [],
            CampaignIds = profile.CampaignIds?.ToList() ?? [],
            ChangedUtc = changedUtc,
        };

        await _auditRecorder.RecordAgentStateAsync(change, context.Actor ?? ContactCenterActor.System, cancellationToken);

        return change;
    }

    /// <inheritdoc/>
    public async Task<AgentStateReason> ResolveReasonAsync(string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var trimmed = reason.Trim();

        // The identifier is what a caller that knows the code passes, and it survives a rename; the name is what
        // the agent screens have always posted, so it is matched second.
        var reasonCode = await _reasonCodeManager.FindByIdAsync(trimmed, cancellationToken) ??
            await _reasonCodeManager.FindByNameAsync(trimmed, cancellationToken);

        return reasonCode is null
            ? new AgentStateReason(null, reason)
            : new AgentStateReason(reasonCode.ItemId, reasonCode.Name);
    }

    /// <summary>
    /// A return to a ready state carries no not-ready reason unless it was asked for with one of its own.
    /// </summary>
    /// <remarks>
    /// The reason on the profile is the reason for the not-ready state it was set with. Only the agent setting a
    /// state replaces it, so every other way back to ready -- a call ending, an offer lapsing, reconciliation -- left
    /// the last break's reason on an agent who was ready again, and every presence broadcast and report read it as
    /// the reason they were available. A reason kept for a not-ready state still waiting to take effect is left
    /// alone: it belongs to that request, not to the state being entered now.
    /// </remarks>
    private static void ClearStaleReasonOnReady(AgentProfile profile, AgentPresenceStatus state, AgentStateChangeContext context)
    {
        if (state != AgentPresenceStatus.Available ||
            !string.IsNullOrEmpty(context.ReasonName) ||
            !string.IsNullOrEmpty(context.ReasonCodeId) ||
            (profile.RequestedPresenceStatus.HasValue && profile.RequestedPresenceStatus != AgentPresenceStatus.Available))
        {
            return;
        }

        profile.PresenceReason = null;
        profile.PresenceReasonCodeId = null;
    }

    private DateTime ResolveChangedUtc(DateTime? requestedUtc, DateTime? previousChangedUtc)
    {
        var changedUtc = DateTime.SpecifyKind(requestedUtc ?? _clock.UtcNow, DateTimeKind.Utc);

        // A change is never dated before the one it follows. A sign-off dated by the last heartbeat can precede a
        // release the sweep itself made a moment earlier, and two nodes' clocks can disagree by a fraction of a
        // second; either would otherwise give a state a negative duration.
        if (previousChangedUtc.HasValue && changedUtc < previousChangedUtc.Value)
        {
            return DateTime.SpecifyKind(previousChangedUtc.Value, DateTimeKind.Utc);
        }

        return changedUtc;
    }
}
