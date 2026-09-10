using System.Security.Claims;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Decides what an agent may transfer a live call to, and turns what they typed into the destination the
/// provider is given. A deployment that curates its transfer destinations replaces the default so the transfer
/// field accepts only curated identifiers rather than any number an agent can type.
/// </summary>
public interface ITransferTargetPolicy
{
    /// <summary>
    /// Resolves and authorizes a transfer target.
    /// </summary>
    /// <param name="rawTarget">What the agent typed or selected.</param>
    /// <param name="user">The agent performing the transfer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The decision, carrying the provider-safe destination when the transfer is allowed.</returns>
    Task<TransferTargetDecision> ResolveAsync(string rawTarget, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
