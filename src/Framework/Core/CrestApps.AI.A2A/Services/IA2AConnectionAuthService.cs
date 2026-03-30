using CrestApps.AI.A2A.Models;

namespace CrestApps.AI.A2A.Services;

public interface IA2AConnectionAuthService
{
    /// <summary>
    /// Builds the HTTP authentication headers for the given A2A connection metadata.
    /// </summary>
    Task<Dictionary<string, string>> BuildHeadersAsync(A2AConnectionMetadata metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configures an HttpClient with the authentication headers for the given connection metadata.
    /// </summary>
    Task ConfigureHttpClientAsync(HttpClient httpClient, A2AConnectionMetadata metadata, CancellationToken cancellationToken = default);
}
