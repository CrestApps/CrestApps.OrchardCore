using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

/// <summary>
/// Default <see cref="ISmsAgentAvailabilityService"/>. Availability is stored in the agent profile's property
/// bag (<see cref="SmsAgentAvailability"/>), so it survives independently of voice presence and needs no schema.
/// </summary>
public sealed class SmsAgentAvailabilityService : ISmsAgentAvailabilityService
{
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsAgentAvailabilityService"/> class.
    /// </summary>
    public SmsAgentAvailabilityService(IAgentProfileManager agentProfileManager, IClock clock)
    {
        _agentProfileManager = agentProfileManager;
        _clock = clock;
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
