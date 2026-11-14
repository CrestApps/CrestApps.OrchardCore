using CrestApps.OrchardCore.AgenticCommerce.Core.Indexes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Stores agent platforms in YesSql.
/// </summary>
public sealed class AgentPlatformStore : DocumentCatalog<AgentPlatform, AgentPlatformIndex>, IAgentPlatformStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentPlatformStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public AgentPlatformStore(ISession session)
        : base(session)
    {
        CollectionName = AgenticCommerceStorage.CollectionName;
    }

    /// <inheritdoc />
    public Task<AgentPlatform> FindByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(apiKeyHash);

        return Session.Query<AgentPlatform, AgentPlatformIndex>(index => index.ApiKeyHash == apiKeyHash, collection: AgenticCommerceStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<AgentPlatform> FindByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientId);

        return Session.Query<AgentPlatform, AgentPlatformIndex>(index => index.OAuthClientId == clientId, collection: AgenticCommerceStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<AgentPlatform> FindByProfileUrlAsync(string profileUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(profileUrl);

        return Session.Query<AgentPlatform, AgentPlatformIndex>(index => index.ProfileUrl == profileUrl, collection: AgenticCommerceStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AgentPlatform> FindByProfileHostAsync(string host, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);

        // The text search narrows it down; the host itself is compared here, so another host that contains this one never
        // matches. The pattern is one value: YesSql turns a concatenation in the expression into SQL, not one parameter.
        var pattern = "://" + host;
        var candidates = await Session.Query<AgentPlatform, AgentPlatformIndex>(index => index.ProfileUrl.Contains(pattern), collection: AgenticCommerceStorage.CollectionName)
            .Take(100)
            .ListAsync(cancellationToken);

        return candidates.FirstOrDefault(platform =>
            Uri.TryCreate(platform.ProfileUrl, UriKind.Absolute, out var uri) && string.Equals(uri.IdnHost, host, StringComparison.OrdinalIgnoreCase));
    }
}
