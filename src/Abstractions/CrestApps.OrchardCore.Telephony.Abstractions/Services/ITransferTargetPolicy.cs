using System.Security.Claims;
using CrestApps.OrchardCore.Telephony.Models;

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

    /// <summary>
    /// Resolves and authorizes the target of a transfer request, which says whether the target is an internal
    /// extension. A policy that does not tell extensions apart decides as it does for the typed target.
    /// </summary>
    /// <param name="request">The transfer request.</param>
    /// <param name="user">The agent performing the transfer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The decision, carrying the provider-safe destination when the transfer is allowed.</returns>
    Task<TransferTargetDecision> ResolveAsync(TransferRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => ResolveAsync(request?.To, user, cancellationToken);
}
