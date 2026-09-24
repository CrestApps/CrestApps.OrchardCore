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
}
