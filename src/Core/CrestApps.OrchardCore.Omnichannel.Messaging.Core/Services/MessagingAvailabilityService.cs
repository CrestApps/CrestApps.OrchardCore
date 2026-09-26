using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Default <see cref="IMessagingAvailabilityService"/>. The volunteered flag is stored in the agent profile's
/// property bag (<see cref="MessagingAgentAvailability"/>), so it survives independently of voice presence and needs
/// no schema; whether the agent is actually there comes from the workspace presence tracker.
/// </summary>
public sealed class MessagingAvailabilityService : IMessagingAvailabilityService
{
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IMessagingPresenceTracker _presenceTracker;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingAvailabilityService"/> class.
    /// </summary>
    /// <param name="agentProfileManager">The agent profile manager.</param>
    /// <param name="presenceTracker">The tracker that knows whose workspace is open.</param>
    /// <param name="clock">The clock.</param>
    public MessagingAvailabilityService(
        IAgentProfileManager agentProfileManager,
        IMessagingPresenceTracker presenceTracker,
        IClock clock)
    {
        _agentProfileManager = agentProfileManager;
        _presenceTracker = presenceTracker;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync(AgentProfile agent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (!Get(agent).Available)
        {
            return false;
        }

        // The flag says they volunteered; the workspace checking in says they are still there to answer. A voice
        // sign-in is deliberately not enough: a pushed conversation is only visible in the workspace, so an agent
        // on the soft phone with the inbox closed would be assigned threads they cannot see.
        return await _presenceTracker.IsPresentAsync(agent.ItemId, cancellationToken);
    }

    /// <inheritdoc/>
    public MessagingAgentAvailability Get(AgentProfile agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        return agent.TryGet<MessagingAgentAvailability>(out var availability) && availability is not null
            ? availability
            : new MessagingAgentAvailability();
    }

    /// <inheritdoc/>
    public async Task<MessagingAgentAvailability> SetAvailableAsync(AgentProfile agent, bool available, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var availability = Get(agent);
        availability.Available = available;
        availability.UpdatedUtc = _clock.UtcNow;

        agent.Put(availability);

        await _agentProfileManager.UpdateAsync(agent, cancellationToken: cancellationToken);

        return availability;
    }
}
