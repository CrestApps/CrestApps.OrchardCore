namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Resolves the SIP address an agent's browser is registered on, so every path that dials an agent picks the
/// same credential — the one that is actually listening.
/// </summary>
public interface ITelnyxAgentEndpointResolver
{
    /// <summary>
    /// Resolves the agent's SIP endpoint.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The SIP URI, or <see langword="null"/> when the agent has no reachable credential.</returns>
    Task<string> ResolveAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the agent's SIP endpoint only when the client registered on it reported the given capability.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="requiredClientCapability">The capability the registered client must have reported.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The SIP URI, or <see langword="null"/> when the agent has no reachable credential or the client listening on it
    /// did not report the capability.
    /// </returns>
    Task<string> ResolveAsync(string userId, string requiredClientCapability, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a leg to <paramref name="unreachableEndpoint"/> was refused as unavailable, and resolves where the
    /// user's phone can be rung instead.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="unreachableEndpoint">The SIP address the refused leg rang.</param>
    /// <param name="requiredClientCapability">
    /// The capability the client registered on the new credential must have reported, or <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// Another of the user's credentials to ring, or <see langword="null"/> when there is none other than the one that
    /// was refused.
    /// </returns>
    Task<TelnyxAgentEndpointRedelivery> ResolveRedeliveryAsync(
        string userId,
        string unreachableEndpoint,
        string requiredClientCapability,
        CancellationToken cancellationToken = default);
}
