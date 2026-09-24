using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IAgentPresenceManager"/>.
/// </summary>
/// <remarks>
/// Every state change made here goes through <see cref="IAgentStateTransitionService"/>, which records it for audit
/// and payroll; the presence events published alongside are unchanged and still drive routing and broadcasts.
/// </remarks>
public sealed partial class AgentPresenceManagerService : IAgentPresenceManager
{
    private static readonly TimeSpan _signInLockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _signInLockExpiration = TimeSpan.FromMinutes(1);

    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentSessionManager _sessionManager;
    private readonly IAgentWorkStateHealingService _agentWorkStateHealingService;
    private readonly IAgentEntitlementPolicy _entitlementPolicy;
    private readonly IAgentStateTransitionService _stateTransitions;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentPresenceManagerService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="sessionManagers">The optional real-time agent session managers.</param>
    /// <param name="agentWorkStateHealingService">The agent state healer.</param>
    /// <param name="entitlementPolicy">The policy that decides which queues and campaigns an agent may join. The
    /// permissive default imposes no restriction; the Agent Entitlements feature replaces it with an enforcing one.</param>
    /// <param name="stateTransitions">The one place agent state is changed and recorded.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="distributedLock">The distributed lock used to serialize sign-in updates.</param>
    /// <param name="clock">The clock used to stamp presence changes.</param>
    /// <param name="logger">The logger.</param>
    public AgentPresenceManagerService(
        IAgentProfileManager agentManager,
        IEnumerable<IAgentSessionManager> sessionManagers,
        IAgentWorkStateHealingService agentWorkStateHealingService,
        IAgentEntitlementPolicy entitlementPolicy,
        IAgentStateTransitionService stateTransitions,
        IContactCenterEventPublisher publisher,
        IDistributedLock distributedLock,
        IClock clock,
        ILogger<AgentPresenceManagerService> logger)
    {
        _agentManager = agentManager;
        _sessionManager = sessionManagers.FirstOrDefault();
        _agentWorkStateHealingService = agentWorkStateHealingService;
        _entitlementPolicy = entitlementPolicy;
        _stateTransitions = stateTransitions;
        _publisher = publisher;
        _distributedLock = distributedLock;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AgentProfile> SignInAsync(string userId, IEnumerable<string> queueIds, IEnumerable<string> campaignIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var selectedQueueIds = queueIds?.Distinct().ToList() ?? [];
        var selectedCampaignIds = campaignIds?.Distinct().ToList() ?? [];

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Signing a Contact Center agent in to {QueueCount} queues and {CampaignCount} campaigns.",
                selectedQueueIds.Count,
                selectedCampaignIds.Count);
        }

        var profile = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        if (profile is not null)
        {
            await _agentWorkStateHealingService.HealForResetAsync(profile.ItemId, cancellationToken);
        }

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            AgentProfileLock.GetKey(userId),
            _signInLockTimeout,
            _signInLockExpiration);

        if (!locked)
        {
            throw new InvalidOperationException($"The Contact Center agent profile for user '{userId}' is currently being updated.");
        }

        await using var acquiredLock = locker;

        profile = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        if (profile is null)
        {
            profile = await _agentManager.NewAsync(cancellationToken: cancellationToken);
            profile.UserId = userId;
            profile.Name = userId;
        }

        var (entitledQueueIds, entitledCampaignIds) = _entitlementPolicy.ResolveMemberships(profile, selectedQueueIds, selectedCampaignIds);

        if (entitledQueueIds.Count == 0 && entitledCampaignIds.Count == 0)
        {
            throw new AgentEntitlementDeniedException(userId);
        }

        var previousStatus = profile.PresenceStatus;

        profile.QueueIds = ApplyCampaignRouting(profile, entitledQueueIds, entitledCampaignIds);
        profile.CampaignIds = entitledCampaignIds;
        profile.RequestedPresenceStatus = null;
        profile.ActiveReservationId = null;

        var actor = ContactCenterActor.Agent(userId);

        var change = await _stateTransitions.TransitionAsync(profile, AgentPresenceStatus.Available, new AgentStateChangeContext
        {
            Actor = actor,
            Source = AgentStateChangeSources.SignIn,
            AgentSessionId = await FindAgentSessionIdAsync(userId, cancellationToken),
        }, cancellationToken);

        AgentPresenceUtilities.ApplyIdleState(profile, _clock.UtcNow);

        await SaveAsync(profile, cancellationToken);
        await SyncSessionMembershipAsync(userId, profile.QueueIds, profile.CampaignIds, cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentSignedIn, profile, previousStatus, actor, change, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Completed Contact Center sign-in for agent '{AgentId}' and user '{UserId}' with presence '{PresenceStatus}'.",
                profile.ItemId.SanitizeLogValue(),
                userId.SanitizeLogValue(),
                profile.PresenceStatus);
        }

        return profile;
    }

    /// <inheritdoc/>
    public async Task<AgentProfile> UpdateMembershipsAsync(
        string userId,
        IEnumerable<string> queueIds,
        IEnumerable<string> campaignIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            AgentProfileLock.GetKey(userId),
            _signInLockTimeout,
            _signInLockExpiration);

        if (!locked)
        {
            throw new InvalidOperationException($"The Contact Center agent profile for user '{userId}' is currently being updated.");
        }

        await using var acquiredLock = locker;

        var profile = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        var (entitledQueueIds, entitledCampaignIds) = _entitlementPolicy.ResolveMemberships(profile, queueIds, campaignIds);

        if (entitledQueueIds.Count == 0 && entitledCampaignIds.Count == 0)
        {
            throw new AgentEntitlementDeniedException(userId);
        }

        var previousStatus = profile.PresenceStatus;

        profile.QueueIds = ApplyCampaignRouting(profile, entitledQueueIds, entitledCampaignIds);
        profile.CampaignIds = entitledCampaignIds;

        // Memberships change here, the state does not, so nothing is recorded as a state change: the sign-in event
        // republished below is what routing listens to for the new queues.
        await _agentManager.UpdateAsync(profile, cancellationToken: cancellationToken);
        await SyncSessionMembershipAsync(userId, profile.QueueIds, profile.CampaignIds, cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentSignedIn, profile, previousStatus, ContactCenterActor.Agent(userId), change: null, cancellationToken);

        return profile;
    }

    /// <summary>
    /// Folds each signed-in campaign's virtual queue into the agent's routing queue set. This is what makes
    /// "sign into a campaign" actually receive that campaign's outbound work: the campaign's queue id is derived
    /// directly from the campaign (it is never stored), and adding it to both the signed-in queues and the allowed
    /// queues lets the existing queue-based routing, membership index, and availability gate treat it like any other
    /// entitled queue. Agents and admins only ever pick campaigns; these queues stay hidden.
    /// </summary>
    private static IList<string> ApplyCampaignRouting(
        AgentProfile profile,
        IList<string> entitledQueueIds,
        IList<string> entitledCampaignIds)
    {
        if (entitledCampaignIds.Count == 0)
        {
            return entitledQueueIds;
        }

        var campaignQueueIds = entitledCampaignIds
            .Select(ContactCenterConstants.CampaignQueue.CreateId)
            .ToList();

        // Entitlement to a campaign implies entitlement to its virtual queue; the membership index and the
        // availability gate both require the queue to appear in the allowed set.
        profile.AllowedQueueIds = profile.AllowedQueueIds
            .Concat(campaignQueueIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return entitledQueueIds
            .Concat(campaignQueueIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc/>
    public Task<AgentProfile> SignOutAsync(string userId, CancellationToken cancellationToken = default)
        => SignOutAsync(userId, context: null, cancellationToken);

    /// <inheritdoc/>
    public async Task<AgentProfile> SignOutAsync(string userId, AgentStateChangeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var profile = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        if (profile is null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Skipped Contact Center sign-out for user '{UserId}' because no agent profile exists.", userId.SanitizeLogValue());
            }

            return null;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Signing Contact Center agent '{AgentId}' for user '{UserId}' out of {QueueCount} queues and {CampaignCount} campaigns.",
                profile.ItemId.SanitizeLogValue(),
                userId.SanitizeLogValue(),
                profile.QueueIds.Count,
                profile.CampaignIds.Count);
        }

        await _agentWorkStateHealingService.HealForResetAsync(profile.ItemId, cancellationToken);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            AgentProfileLock.GetKey(userId),
            _signInLockTimeout,
            _signInLockExpiration);

        if (!locked)
        {
            throw new InvalidOperationException($"The Contact Center agent profile for user '{userId}' is currently being updated.");
        }

        await using var acquiredLock = locker;

        profile = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        var previousStatus = profile.PresenceStatus;

        profile.PresenceReason = null;
        profile.PresenceReasonCodeId = null;
        profile.RequestedPresenceStatus = null;
        profile.QueueIds = [];
        profile.CampaignIds = [];

        var actor = context?.Actor ?? ContactCenterActor.Agent(userId);

        var change = await _stateTransitions.TransitionAsync(profile, AgentPresenceStatus.Offline, new AgentStateChangeContext
        {
            Actor = actor,
            Source = context?.Source ?? AgentStateChangeSources.SignOut,
            ReasonCodeId = context?.ReasonCodeId,
            ReasonName = context?.ReasonName,
            AgentSessionId = context?.AgentSessionId ?? await FindAgentSessionIdAsync(userId, cancellationToken),
            ChangedUtc = context?.ChangedUtc,
        }, cancellationToken);

        AgentPresenceUtilities.ApplyIdleState(profile, _clock.UtcNow);

        await _agentManager.UpdateAsync(profile, cancellationToken: cancellationToken);
        await SyncSessionMembershipAsync(userId, profile.QueueIds, profile.CampaignIds, cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentSignedOut, profile, previousStatus, actor, change, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Completed Contact Center sign-out for agent '{AgentId}'.", profile.ItemId.SanitizeLogValue());
        }

        return profile;
    }

    /// <inheritdoc/>
    public Task<AgentProfile> MarkOfflineAsync(string userId, string reason, CancellationToken cancellationToken = default)
        => MarkOfflineAsync(userId, reason, context: null, cancellationToken);

    /// <inheritdoc/>
    public async Task<AgentProfile> MarkOfflineAsync(string userId, string reason, AgentStateChangeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var profile = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        if (profile.PresenceStatus == AgentPresenceStatus.Offline)
        {
            return profile;
        }

        // Release any work the absent agent was holding so a reservation cannot outlive the connection that owned
        // it, exactly as an explicit sign-out does. Only the memberships survive.
        await _agentWorkStateHealingService.HealForResetAsync(profile.ItemId, cancellationToken);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            AgentProfileLock.GetKey(userId),
            _signInLockTimeout,
            _signInLockExpiration);

        if (!locked)
        {
            throw new InvalidOperationException($"The Contact Center agent profile for user '{userId}' is currently being updated.");
        }

        await using var acquiredLock = locker;

        profile = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        var previousStatus = profile.PresenceStatus;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Taking Contact Center agent '{AgentId}' for user '{UserId}' offline for reason '{Reason}', keeping {QueueCount} queue and {CampaignCount} campaign memberships.",
                profile.ItemId.SanitizeLogValue(),
                userId.SanitizeLogValue(),
                reason.SanitizeLogValue(),
                profile.QueueIds.Count,
                profile.CampaignIds.Count);
        }

        profile.PresenceReason = reason;
        profile.PresenceReasonCodeId = null;
        profile.RequestedPresenceStatus = null;

        // Going offline this way is the platform noticing an agent it can no longer reach, so by default it is the
        // platform's change, and it is dated by when the agent was last heard from when the caller knows that.
        var actor = context?.Actor ?? ContactCenterActor.System;

        var change = await _stateTransitions.TransitionAsync(profile, AgentPresenceStatus.Offline, new AgentStateChangeContext
        {
            Actor = actor,
            Source = context?.Source ?? AgentStateChangeSources.SessionExpired,
            ReasonName = reason,
            AgentSessionId = context?.AgentSessionId,
            ChangedUtc = context?.ChangedUtc,
        }, cancellationToken);

        AgentPresenceUtilities.ApplyIdleState(profile, _clock.UtcNow);

        await SaveAsync(profile, cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentPresenceChanged, profile, previousStatus, actor, change, cancellationToken);

        return profile;
    }

    /// <inheritdoc/>
    public Task<AgentProfile> SetPresenceAsync(string userId, AgentPresenceStatus status, string reason, CancellationToken cancellationToken = default)
        => SetPresenceAsync(userId, status, reason, context: null, cancellationToken);

    /// <inheritdoc/>
    public async Task<AgentProfile> SetPresenceAsync(
        string userId,
        AgentPresenceStatus status,
        string reason,
        AgentStateChangeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var profile = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        if (profile is null)
        {
            profile = await _agentManager.NewAsync(cancellationToken: cancellationToken);
            profile.UserId = userId;
            profile.Name = userId;
        }
        else if (!CanApplyPresenceNow(profile))
        {
            // The agent is parked in an on-call presence state (Reserved/Busy/WrapUp or holding a reservation).
            // Reconcile against provider truth before deferring the requested change so a call that no longer
            // exists on the provider cannot leave the agent stuck and unable to return to a ready state. Live
            // provider-backed calls are preserved by the healer, so a genuine in-progress call still defers the
            // change as before.
            await _agentWorkStateHealingService.HealForResetAsync(profile.ItemId, cancellationToken);
        }

        var resolvedReason = await ResolveReasonAsync(reason, context, cancellationToken);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            AgentProfileLock.GetKey(userId),
            _signInLockTimeout,
            _signInLockExpiration);

        if (!locked)
        {
            throw new InvalidOperationException($"The Contact Center agent profile for user '{userId}' is currently being updated.");
        }

        await using var acquiredLock = locker;

        profile = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        if (profile is null)
        {
            profile = await _agentManager.NewAsync(cancellationToken: cancellationToken);
            profile.UserId = userId;
            profile.Name = userId;
        }

        var previousStatus = profile.PresenceStatus;
        var previousReasonCodeId = profile.PresenceReasonCodeId;
        var previousReason = profile.PresenceReason;
        var targetStatus = previousStatus;

        if (status == AgentPresenceStatus.RequestBreak)
        {
            profile.RequestedPresenceStatus = AgentPresenceStatus.Break;

            if (CanApplyPresenceNow(profile))
            {
                targetStatus = AgentPresenceStatus.Break;
                profile.RequestedPresenceStatus = null;
            }
        }
        else if (CanApplyPresenceNow(profile))
        {
            targetStatus = status;
            profile.RequestedPresenceStatus = null;
        }
        else
        {
            profile.RequestedPresenceStatus = status;
        }

        var deferred = profile.RequestedPresenceStatus.HasValue;

        if (deferred)
        {
            // Marks the pending state as one somebody asked for, so the audit records it taking effect as a
            // request applied rather than as the work simply ending.
            profile.PresenceRequestedUtc = _clock.UtcNow;
        }

        profile.PresenceReason = resolvedReason?.Name ?? reason;
        profile.PresenceReasonCodeId = resolvedReason?.ReasonCodeId;

        // A deferred request leaves the agent where they are, so there is nothing to record until it takes effect.
        // A reason change within the same state is recorded: a switch from one break to another is a new break.
        var reasonChanged = !string.Equals(previousReasonCodeId, profile.PresenceReasonCodeId, StringComparison.Ordinal) ||
            !string.Equals(previousReason, profile.PresenceReason, StringComparison.Ordinal);

        var actor = context?.Actor ?? ContactCenterActor.Agent(userId);

        var change = await _stateTransitions.TransitionAsync(profile, targetStatus, new AgentStateChangeContext
        {
            Actor = actor,
            Source = context?.Source ?? AgentStateChangeSources.SetState,
            ReasonCodeId = profile.PresenceReasonCodeId,
            ReasonName = profile.PresenceReason,
            AgentSessionId = context?.AgentSessionId ?? await FindAgentSessionIdAsync(userId, cancellationToken),
            RecordWhenUnchanged = !deferred && reasonChanged,
        }, cancellationToken);

        AgentPresenceUtilities.ApplyIdleState(profile, _clock.UtcNow);

        await SaveAsync(profile, cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentPresenceChanged, profile, previousStatus, actor, change, cancellationToken);

        return profile;
    }

    private async Task<AgentStateReason> ResolveReasonAsync(string reason, AgentStateChangeContext context, CancellationToken cancellationToken)
    {
        // A caller that already knows the reason code passes it; otherwise the reason given is matched against the
        // configured codes, by identifier first and then by name, which is what the agent screens post.
        if (!string.IsNullOrEmpty(context?.ReasonCodeId))
        {
            return await _stateTransitions.ResolveReasonAsync(context.ReasonCodeId, cancellationToken) ??
                await _stateTransitions.ResolveReasonAsync(reason, cancellationToken);
        }

        return await _stateTransitions.ResolveReasonAsync(reason, cancellationToken);
    }

    private static bool CanApplyPresenceNow(AgentProfile profile)
    {
        return string.IsNullOrEmpty(profile.ActiveReservationId) &&
            profile.PresenceStatus is not AgentPresenceStatus.Reserved and not AgentPresenceStatus.Busy and not AgentPresenceStatus.WrapUp;
    }

    private async Task SaveAsync(AgentProfile profile, CancellationToken cancellationToken)
    {
        var existing = await _agentManager.FindByIdAsync(profile.ItemId, cancellationToken);

        if (existing is null)
        {
            await _agentManager.CreateAsync(profile, cancellationToken: cancellationToken);
        }
        else
        {
            await _agentManager.UpdateAsync(profile, cancellationToken: cancellationToken);
        }
    }

    private async Task<string> FindAgentSessionIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (_sessionManager is null || string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var session = await _sessionManager.FindByUserIdAsync(userId, cancellationToken);

        return session?.ItemId;
    }

    private async Task SyncSessionMembershipAsync(
        string userId,
        IEnumerable<string> queueIds,
        IEnumerable<string> campaignIds,
        CancellationToken cancellationToken)
    {
        if (_sessionManager is null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipped Contact Center live-session membership synchronization for user '{UserId}' because no session manager is registered.",
                    userId.SanitizeLogValue());
            }

            return;
        }

        var session = await _sessionManager.FindByUserIdAsync(userId, cancellationToken);

        if (session is null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "No live Contact Center agent session exists for user '{UserId}'; profile memberships were saved but no connected session was updated.",
                    userId.SanitizeLogValue());
            }

            return;
        }

        session.QueueIds = queueIds?.Distinct().ToList() ?? [];
        session.CampaignIds = campaignIds?.Distinct().ToList() ?? [];
        session.ModifiedUtc = _clock.UtcNow;

        await _sessionManager.UpdateAsync(session, cancellationToken: cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Synchronized Contact Center session '{SessionId}' for user '{UserId}' with {QueueCount} queues and {CampaignCount} campaigns.",
                session.ItemId.SanitizeLogValue(),
                userId.SanitizeLogValue(),
                session.QueueIds.Count,
                session.CampaignIds.Count);
        }
    }

    /// <summary>
    /// Publishes the presence event a change is broadcast and routed by.
    /// </summary>
    /// <remarks>
    /// It names the same actor as the state change it accompanies, and is dated by the same instant: the time the
    /// change took effect, which for a change a provider event caused is the provider's time for that event. The
    /// agent is the subject, carried by the aggregate and in the payload, never in the actor.
    /// </remarks>
    private Task PublishAsync(
        string eventType,
        AgentProfile profile,
        AgentPresenceStatus previousStatus,
        ContactCenterActor actor,
        AgentStateChangedEventData change,
        CancellationToken cancellationToken)
    {
        var changedUtc = profile.PresenceChangedUtc ?? _clock.UtcNow;
        var interactionEvent = new InteractionEvent
        {
            EventType = eventType,
            AggregateType = nameof(AgentProfile),
            AggregateId = profile.ItemId,
            ActorId = actor.Id ?? ContactCenterConstants.SystemActor,
            ActorType = actor.Type,
            SourceComponent = ContactCenterConstants.Components.Agents,

            // A presence event that accompanies a state change happened when the change did. One that changes no
            // state -- a request deferred behind a call, new memberships -- happened now.
            OccurredUtc = change?.ChangedUtc ?? _clock.UtcNow,
        };

        interactionEvent.SetData(new AgentPresenceChangedEventData
        {
            AgentId = profile.ItemId,
            UserId = profile.UserId,
            PreviousStatus = previousStatus,
            CurrentStatus = profile.PresenceStatus,
            RequestedStatus = profile.RequestedPresenceStatus,
            Reason = profile.PresenceReason,
            QueueIds = profile.QueueIds.ToList(),
            CampaignIds = profile.CampaignIds.ToList(),
            ChangedUtc = changedUtc,
        });

        return _publisher.PublishAsync(interactionEvent, cancellationToken);
    }
}
