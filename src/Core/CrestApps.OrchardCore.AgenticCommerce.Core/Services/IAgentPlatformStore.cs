using CrestApps.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Stores agent platforms.
/// </summary>
public interface IAgentPlatformStore : ICatalog<AgentPlatform>
{
    /// <summary>
    /// Returns the platform whose API key has a hash, or <see langword="null"/>.
    /// </summary>
    /// <param name="apiKeyHash">The key's hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AgentPlatform> FindByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the platform with an OAuth client ID, or <see langword="null"/>.
    /// </summary>
    /// <param name="clientId">The client ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AgentPlatform> FindByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the platform with a UCP profile address, or <see langword="null"/>.
    /// </summary>
    /// <param name="profileUrl">The profile's address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AgentPlatform> FindByProfileUrlAsync(string profileUrl, CancellationToken cancellationToken = default);
}
