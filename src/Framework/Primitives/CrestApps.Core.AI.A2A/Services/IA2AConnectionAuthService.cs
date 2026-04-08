using CrestApps.Core.AI.A2A.Models;

namespace CrestApps.Core.AI.A2A.Services;

/// <summary>
/// Provides authentication services for Agent-to-Agent (A2A) protocol connections,
/// building HTTP headers and configuring clients for secure inter-agent communication.
/// </summary>
public interface IA2AConnectionAuthService
{
    /// <summary>
    /// Builds the HTTP authentication headers for the given A2A connection metadata.
    /// </summary>
    /// <param name="metadata">The A2A connection metadata containing authentication configuration.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A dictionary of HTTP header name-value pairs for authentication.</returns>
    Task<Dictionary<string, string>> BuildHeadersAsync(A2AConnectionMetadata metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configures an HttpClient with the authentication headers for the given connection metadata.
    /// </summary>
    /// <param name="httpClient">The HTTP client to configure.</param>
    /// <param name="metadata">The A2A connection metadata containing authentication configuration.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ConfigureHttpClientAsync(HttpClient httpClient, A2AConnectionMetadata metadata, CancellationToken cancellationToken = default);
}
