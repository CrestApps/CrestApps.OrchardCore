using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Default <see cref="ISmsAgentAvailabilityService"/>. Availability is stored in the agent profile's property
/// bag (<see cref="SmsAgentAvailability"/>), so it survives independently of voice presence and needs no schema.
/// </summary>
public sealed class SmsAgentAvailabilityService : ISmsAgentAvailabilityService
{
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IAgentSessionManager _sessionManager;
    private readonly AgentAvailabilityOptions _availabilityOptions;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsAgentAvailabilityService"/> class.
    /// </summary>
    public SmsAgentAvailabilityService(
        IAgentProfileManager agentProfileManager,
        IAgentSessionManager sessionManager,
        IOptions<AgentAvailabilityOptions> availabilityOptions,
        IClock clock)
    {
        _agentProfileManager = agentProfileManager;
        _sessionManager = sessionManager;
        _availabilityOptions = availabilityOptions.Value;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync(AgentProfile agent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (!Get(agent).Available || string.IsNullOrEmpty(agent.UserId))
        {
            return false;
        }

        var session = await _sessionManager.FindByUserIdAsync(agent.UserId, cancellationToken);

        // The same heartbeat timeout voice presence uses, so an agent is not live for one channel and gone for
        // the other.
        return session is not null
            && session.IsOnline
            && session.LastHeartbeatUtc is not null
            && session.LastHeartbeatUtc >= _clock.UtcNow - _availabilityOptions.HeartbeatTimeout;
    }

    /// <inheritdoc/>
    public SmsAgentAvailability Get(AgentProfile agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        return agent.TryGet<SmsAgentAvailability>(out var availability) && availability is not null
            ? availability
            : new SmsAgentAvailability();
    }

    /// <inheritdoc/>
    public async Task<SmsAgentAvailability> SetAvailableAsync(AgentProfile agent, bool available, CancellationToken cancellationToken = default)
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
