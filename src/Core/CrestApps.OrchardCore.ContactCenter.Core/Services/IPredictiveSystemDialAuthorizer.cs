using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Decides whether a campaign dial that names no agent may be placed. Every other dial is placed for the agent it names,
/// whose identity the dial executor checks; a call placed before any agent is reserved is the one exception, and only an
/// over-dialing Predictive profile may make it.
/// </summary>
/// <remarks>
/// Registered by the Paced Dialing feature. With none registered, a dial without an agent is refused.
/// </remarks>
public interface IPredictiveSystemDialAuthorizer
{
    /// <summary>
    /// Whether the dial, which names no agent, is a call an over-dialing Predictive profile placed for its campaign.
    /// </summary>
    /// <param name="command">The durable dial command.</param>
    /// <param name="request">The dial request the command carries.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the dial may be placed without an agent.</returns>
    Task<bool> IsAuthorizedAsync(ProviderCommand command, ContactCenterDialRequest request, CancellationToken cancellationToken = default);
}
