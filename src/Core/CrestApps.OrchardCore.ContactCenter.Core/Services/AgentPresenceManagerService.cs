using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IAgentPresenceManager"/>.
/// </summary>
public sealed class AgentPresenceManagerService : IAgentPresenceManager
{
    private static readonly TimeSpan _signInLockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _signInLockExpiration = TimeSpan.FromMinutes(1);

    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentSessionManager _sessionManager;
    private readonly IAgentWorkStateHealingService _agentWorkStateHealingService;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IContactCenterRealTimeNotifier _realTimeNotifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentPresenceManagerService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="sessionManagers">The optional real-time agent session managers.</param>
    /// <param name="agentWorkStateHealingServices">The optional agent state healers.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="distributedLock">The distributed lock used to serialize sign-in updates.</param>
    /// <param name="clock">The clock used to stamp presence changes.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="realTimeNotifiers">The optional real-time notifier used to revoke active queue subscriptions.</param>
    public AgentPresenceManagerService(
        IAgentProfileManager agentManager,
        IEnumerable<IAgentSessionManager> sessionManagers,
        IEnumerable<IAgentWorkStateHealingService> agentWorkStateHealingServices,
        IContactCenterEventPublisher publisher,
        IDistributedLock distributedLock,
        IClock clock,
        ILogger<AgentPresenceManagerService> logger,
        IEnumerable<IContactCenterRealTimeNotifier> realTimeNotifiers = null)
    {
        _agentManager = agentManager;
        _sessionManager = sessionManagers.FirstOrDefault();
        _agentWorkStateHealingService = agentWorkStateHealingServices.FirstOrDefault();
        _publisher = publisher;
        _distributedLock = distributedLock;
        _clock = clock;
        _logger = logger;
        _realTimeNotifier = realTimeNotifiers?.FirstOrDefault();
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

        if (profile is not null && _agentWorkStateHealingService is not null)
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

        var entitledQueueIds = AgentEntitlementUtilities.FilterEntitled(selectedQueueIds, profile.AllowedQueueIds);
        var entitledCampaignIds = AgentEntitlementUtilities.FilterEntitled(selectedCampaignIds, profile.AllowedCampaignIds);

        if (entitledQueueIds.Count == 0 && entitledCampaignIds.Count == 0)
        {
            throw new AgentEntitlementDeniedException(userId);
        }

        var previousStatus = profile.PresenceStatus;

        profile.QueueIds = entitledQueueIds;
        profile.CampaignIds = entitledCampaignIds;
        profile.PresenceStatus = AgentPresenceStatus.Available;
        profile.RequestedPresenceStatus = null;
        profile.PresenceChangedUtc = _clock.UtcNow;
        profile.ActiveReservationId = null;

        await SaveAsync(profile, cancellationToken);
        await SyncSessionMembershipAsync(userId, profile.QueueIds, profile.CampaignIds, cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentSignedIn, profile, previousStatus, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Completed Contact Center sign-in for agent '{AgentId}' and user '{UserId}' with presence '{PresenceStatus}'.",
                profile.ItemId,
                userId,
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

        var entitledQueueIds = AgentEntitlementUtilities.FilterEntitled(queueIds, profile.AllowedQueueIds);
        var entitledCampaignIds = AgentEntitlementUtilities.FilterEntitled(campaignIds, profile.AllowedCampaignIds);

        if (entitledQueueIds.Count == 0 && entitledCampaignIds.Count == 0)
        {
            throw new AgentEntitlementDeniedException(userId);
        }

        var previousStatus = profile.PresenceStatus;

        profile.QueueIds = entitledQueueIds;
        profile.CampaignIds = entitledCampaignIds;

        await _agentManager.UpdateAsync(profile, cancellationToken: cancellationToken);
        await SyncSessionMembershipAsync(userId, profile.QueueIds, profile.CampaignIds, cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentSignedIn, profile, previousStatus, cancellationToken);

        return profile;
    }

    /// <inheritdoc/>
    public async Task<AgentProfile> SignOutAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var profile = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        if (profile is null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Skipped Contact Center sign-out for user '{UserId}' because no agent profile exists.", userId);
            }

            return null;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Signing Contact Center agent '{AgentId}' for user '{UserId}' out of {QueueCount} queues and {CampaignCount} campaigns.",
                profile.ItemId,
                userId,
                profile.QueueIds.Count,
                profile.CampaignIds.Count);
        }

        if (_agentWorkStateHealingService is not null)
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
            return null;
        }

        var previousStatus = profile.PresenceStatus;

        profile.PresenceStatus = AgentPresenceStatus.Offline;
        profile.PresenceReason = null;
        profile.RequestedPresenceStatus = null;
        profile.PresenceChangedUtc = _clock.UtcNow;
        profile.QueueIds = [];
        profile.CampaignIds = [];

        await _agentManager.UpdateAsync(profile, cancellationToken: cancellationToken);
        await SyncSessionMembershipAsync(userId, profile.QueueIds, profile.CampaignIds, cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentSignedOut, profile, previousStatus, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Completed Contact Center sign-out for agent '{AgentId}'.", profile.ItemId);
        }

        return profile;
    }

    /// <inheritdoc/>
    public async Task<AgentProfile> SetPresenceAsync(string userId, AgentPresenceStatus status, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var profile = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        if (profile is null)
        {
            profile = await _agentManager.NewAsync(cancellationToken: cancellationToken);
            profile.UserId = userId;
            profile.Name = userId;
        }
        else if (_agentWorkStateHealingService is not null && !CanApplyPresenceNow(profile))
        {
            // The agent is parked in an on-call presence state (Reserved/Busy/WrapUp or holding a reservation).
            // Reconcile against provider truth before deferring the requested change so a call that no longer
            // exists on the provider cannot leave the agent stuck and unable to return to a ready state. Live
            // provider-backed calls are preserved by the healer, so a genuine in-progress call still defers the
            // change as before.
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

        var previousStatus = profile.PresenceStatus;

        if (status == AgentPresenceStatus.RequestBreak)
        {
            profile.RequestedPresenceStatus = AgentPresenceStatus.Break;

            if (CanApplyPresenceNow(profile))
            {
                profile.PresenceStatus = AgentPresenceStatus.Break;
                profile.RequestedPresenceStatus = null;
            }
        }
        else if (CanApplyPresenceNow(profile))
        {
            profile.PresenceStatus = status;
            profile.RequestedPresenceStatus = null;
        }
        else
        {
            profile.RequestedPresenceStatus = status;
        }

        profile.PresenceReason = reason;
        profile.PresenceChangedUtc = _clock.UtcNow;

        await SaveAsync(profile, cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentPresenceChanged, profile, previousStatus, cancellationToken);

        return profile;
    }

    /// <inheritdoc/>
    public async Task<AgentProfile> StartWrapUpAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        var profile = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            AgentProfileLock.GetKey(profile.UserId),
            _signInLockTimeout,
            _signInLockExpiration);

        if (!locked)
        {
            throw new InvalidOperationException($"The Contact Center agent profile for user '{profile.UserId}' is currently being updated.");
        }

        await using var acquiredLock = locker;

        profile = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        if (profile.PresenceStatus == AgentPresenceStatus.WrapUp &&
            string.IsNullOrWhiteSpace(profile.ActiveReservationId))
        {
            return profile;
        }

        var previousStatus = profile.PresenceStatus;

        profile.PresenceStatus = AgentPresenceStatus.WrapUp;
        profile.ActiveReservationId = null;
        profile.PresenceChangedUtc = _clock.UtcNow;

        await _agentManager.UpdateAsync(profile, cancellationToken: cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentPresenceChanged, profile, previousStatus, cancellationToken);

        return profile;
    }

    /// <inheritdoc/>
    public async Task<AgentProfile> CompleteWorkAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        var profile = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            AgentProfileLock.GetKey(profile.UserId),
            _signInLockTimeout,
            _signInLockExpiration);

        if (!locked)
        {
            throw new InvalidOperationException($"The Contact Center agent profile for user '{profile.UserId}' is currently being updated.");
        }

        await using var acquiredLock = locker;

        profile = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        var previousStatus = profile.PresenceStatus;

        profile.PresenceStatus = profile.RequestedPresenceStatus ?? AgentPresenceUtilities.ResolveDefaultReadyState(profile);
        profile.RequestedPresenceStatus = null;
        profile.ActiveReservationId = null;
        profile.PresenceChangedUtc = _clock.UtcNow;

        await _agentManager.UpdateAsync(profile, cancellationToken: cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentPresenceChanged, profile, previousStatus, cancellationToken);

        return profile;
    }

    /// <inheritdoc/>
    public async Task<AgentProfile> UpdateEntitlementsAsync(
        string agentId,
        IEnumerable<string> allowedQueueIds,
        IEnumerable<string> allowedCampaignIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        var profile = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            AgentProfileLock.GetKey(profile.UserId),
            _signInLockTimeout,
            _signInLockExpiration);

        if (!locked)
        {
            throw new InvalidOperationException($"The Contact Center agent profile for user '{profile.UserId}' is currently being updated.");
        }

        await using var acquiredLock = locker;

        profile = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        profile.AllowedQueueIds = AgentEntitlementUtilities.NormalizeIds(allowedQueueIds);
        profile.AllowedCampaignIds = AgentEntitlementUtilities.NormalizeIds(allowedCampaignIds);

        var previousQueueIds = profile.QueueIds.ToList();
        var previousCampaignIds = profile.CampaignIds.ToList();
        var prunedQueueIds = AgentEntitlementUtilities.FilterEntitled(profile.QueueIds, profile.AllowedQueueIds);
        var prunedCampaignIds = AgentEntitlementUtilities.FilterEntitled(profile.CampaignIds, profile.AllowedCampaignIds);

        var membershipChanged = !prunedQueueIds.SequenceEqual(profile.QueueIds, StringComparer.OrdinalIgnoreCase) ||
            !prunedCampaignIds.SequenceEqual(profile.CampaignIds, StringComparer.OrdinalIgnoreCase);

        profile.QueueIds = prunedQueueIds;
        profile.CampaignIds = prunedCampaignIds;

        await _agentManager.UpdateAsync(profile, cancellationToken: cancellationToken);
        var removedQueueIds = previousQueueIds
            .Except(profile.QueueIds, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var removedCampaignIds = previousCampaignIds
            .Except(profile.CampaignIds, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await PublishEntitlementsChangedAsync(
            profile,
            removedQueueIds,
            removedCampaignIds,
            cancellationToken);

        if (membershipChanged)
        {
            await SyncSessionMembershipAsync(profile.UserId, profile.QueueIds, profile.CampaignIds, cancellationToken);

            if (_realTimeNotifier is not null)
            {
                await _realTimeNotifier.NotifyAgentMembershipChangedAsync(
                    profile.UserId,
                    removedQueueIds,
                    cancellationToken);
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Pruned unauthorized Contact Center live queue or campaign membership for agent '{AgentId}' after manager entitlement changes.",
                    profile.ItemId);
            }
        }

        return profile;
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
                    userId);
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
                    userId);
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
                session.ItemId,
                userId,
                session.QueueIds.Count,
                session.CampaignIds.Count);
        }
    }

    private Task PublishAsync(
        string eventType,
        AgentProfile profile,
        AgentPresenceStatus previousStatus,
        CancellationToken cancellationToken)
    {
        var interactionEvent = new InteractionEvent
        {
            EventType = eventType,
            AggregateType = nameof(AgentProfile),
            AggregateId = profile.ItemId,
            ActorId = profile.UserId,
            SourceComponent = ContactCenterConstants.Components.Agents,
        };

        interactionEvent.SetData(new AgentPresenceChangedEventData
        {
            PreviousStatus = previousStatus,
            CurrentStatus = profile.PresenceStatus,
            RequestedStatus = profile.RequestedPresenceStatus,
            Reason = profile.PresenceReason,
            QueueIds = profile.QueueIds.ToList(),
            CampaignIds = profile.CampaignIds.ToList(),
            ChangedUtc = profile.PresenceChangedUtc ?? _clock.UtcNow,
        });

        return _publisher.PublishAsync(interactionEvent, cancellationToken);
    }

    private Task PublishEntitlementsChangedAsync(
        AgentProfile profile,
        IEnumerable<string> removedQueueIds,
        IEnumerable<string> removedCampaignIds,
        CancellationToken cancellationToken)
    {
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.AgentEntitlementsChanged,
            AggregateType = nameof(AgentProfile),
            AggregateId = profile.ItemId,
            ActorId = ContactCenterConstants.SystemActor,
            SourceComponent = ContactCenterConstants.Components.Agents,
        };

        interactionEvent.SetData(new AgentEntitlementsChangedEventData
        {
            AllowedQueueIds = profile.AllowedQueueIds.ToList(),
            AllowedCampaignIds = profile.AllowedCampaignIds.ToList(),
            RemovedQueueIds = removedQueueIds.ToList(),
            RemovedCampaignIds = removedCampaignIds.ToList(),
        });

        return _publisher.PublishAsync(interactionEvent, cancellationToken);
    }

}
