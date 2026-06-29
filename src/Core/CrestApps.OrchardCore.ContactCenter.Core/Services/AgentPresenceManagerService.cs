using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IAgentPresenceManager"/>.
/// </summary>
public sealed class AgentPresenceManagerService : IAgentPresenceManager
{
    private readonly IAgentProfileManager _agentManager;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentPresenceManagerService"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="clock">The clock used to stamp presence changes.</param>
    public AgentPresenceManagerService(
        IAgentProfileManager agentManager,
        IContactCenterEventPublisher publisher,
        IClock clock)
    {
        _agentManager = agentManager;
        _publisher = publisher;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<AgentProfile> SignInAsync(string userId, IEnumerable<string> queueIds, IEnumerable<string> campaignIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

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
            return null;
        }

        profile.PresenceStatus = status;
        profile.PresenceReason = reason;
        profile.PresenceChangedUtc = _clock.UtcNow;

        await _agentManager.UpdateAsync(profile, cancellationToken: cancellationToken);
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
}
