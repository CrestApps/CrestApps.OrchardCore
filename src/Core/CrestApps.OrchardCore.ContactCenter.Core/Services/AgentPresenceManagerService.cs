using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
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
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentPresenceManagerService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="distributedLock">The distributed lock used to serialize sign-in updates.</param>
    /// <param name="clock">The clock used to stamp presence changes.</param>
    public AgentPresenceManagerService(
        IAgentProfileManager agentManager,
        IContactCenterEventPublisher publisher,
        IDistributedLock distributedLock,
        IClock clock)
    {
        _agentManager = agentManager;
        _publisher = publisher;
        _distributedLock = distributedLock;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<AgentProfile> SignInAsync(string userId, IEnumerable<string> queueIds, IEnumerable<string> campaignIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetSignInLockKey(userId),
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
            profile = await _agentManager.NewAsync(cancellationToken: cancellationToken);
            profile.UserId = userId;
            profile.Name = userId;
        }

        profile.QueueIds = queueIds?.Distinct().ToList() ?? [];
        profile.CampaignIds = campaignIds?.Distinct().ToList() ?? [];
        profile.PresenceStatus = AgentPresenceStatus.Available;
        profile.PresenceChangedUtc = _clock.UtcNow;
        profile.ActiveReservationId = null;

        await SaveAsync(profile, cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentSignedIn, profile, cancellationToken);

        return profile;
    }

    /// <inheritdoc/>
    public async Task<AgentProfile> SignOutAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var profile = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        profile.PresenceStatus = AgentPresenceStatus.Offline;
        profile.PresenceChangedUtc = _clock.UtcNow;

        await _agentManager.UpdateAsync(profile, cancellationToken: cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentSignedOut, profile, cancellationToken);

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

        profile.PresenceStatus = status;
        profile.PresenceReason = reason;
        profile.PresenceChangedUtc = _clock.UtcNow;

        await SaveAsync(profile, cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentPresenceChanged, profile, cancellationToken);

        return profile;
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

    private Task PublishAsync(string eventType, AgentProfile profile, CancellationToken cancellationToken)
    {
        return _publisher.PublishAsync(new InteractionEvent
        {
            EventType = eventType,
            AggregateType = nameof(AgentProfile),
            AggregateId = profile.ItemId,
            ActorId = profile.UserId,
            SourceComponent = ContactCenterConstants.Components.Agents,
        }, cancellationToken);
    }

    private static string GetSignInLockKey(string userId)
    {
        return $"ContactCenterAgentSignIn:{userId}";
    }
}
