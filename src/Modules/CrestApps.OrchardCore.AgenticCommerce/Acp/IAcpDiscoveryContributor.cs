using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Acp;

/// <summary>
/// Adds to the store's ACP discovery document at <c>/.well-known/acp.json</c>, such as a transport an enabled feature
/// serves.
/// </summary>
public interface IAcpDiscoveryContributor
{
    /// <summary>
    /// Adds to the discovery document.
    /// </summary>
    /// <param name="document">The discovery document.</param>
    /// <param name="baseUrl">The tenant's base address, without a trailing slash.</param>
    Task ContributeAsync(JsonObject document, string baseUrl);
}
