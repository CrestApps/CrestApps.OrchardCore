using CrestApps.OrchardCore.AgenticCommerce.Core.Indexes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Stores agent checkouts in YesSql.
/// </summary>
public sealed class AgentCheckoutStore : DocumentCatalog<AgentCheckout, AgentCheckoutIndex>, IAgentCheckoutStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentCheckoutStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public AgentCheckoutStore(ISession session)
        : base(session)
    {
        CollectionName = AgenticCommerceStorage.CollectionName;
    }

    /// <inheritdoc />
    protected override bool CheckConcurrency
        => true;

    /// <inheritdoc />
    public Task<AgentCheckout> FindByOrderIdAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(orderId);

        return Session.Query<AgentCheckout, AgentCheckoutIndex>(index => index.OrderId == orderId, collection: AgenticCommerceStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<AgentCheckout> FindBySessionIdAsync(string checkoutSessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(checkoutSessionId);

        return Session.Query<AgentCheckout, AgentCheckoutIndex>(index => index.CheckoutSessionId == checkoutSessionId, collection: AgenticCommerceStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentCheckout>> ListExpiredAsync(DateTime utcNow, int take, CancellationToken cancellationToken = default)
    {
        var checkouts = await Session.Query<AgentCheckout, AgentCheckoutIndex>(index => !index.IsTerminal && index.ExpiresUtc < utcNow, collection: AgenticCommerceStorage.CollectionName)
            .OrderBy(index => index.ExpiresUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return checkouts.ToList();
    }

    /// <inheritdoc />
    public Task<int> CountOpenAsync(string platformId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(platformId);

        // Unregistered platforms share one allowance: what any of them has open counts for all of them.
        if (platformId.StartsWith(AgentPlatform.UnregisteredIdPrefix, StringComparison.Ordinal))
        {
            return Session.QueryIndex<AgentCheckoutIndex>(index => index.PlatformId.StartsWith(AgentPlatform.UnregisteredIdPrefix) && !index.IsTerminal, collection: AgenticCommerceStorage.CollectionName)
                .CountAsync(cancellationToken);
        }

        return Session.QueryIndex<AgentCheckoutIndex>(index => index.PlatformId == platformId && !index.IsTerminal, collection: AgenticCommerceStorage.CollectionName)
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountOpenFromSameOriginAsync(string platformId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(platformId);

        if (!platformId.StartsWith(AgentPlatform.UnregisteredIdPrefix, StringComparison.Ordinal) || platformId.Length < AgentPlatform.UnregisteredOriginKeyLength)
        {
            return Session.QueryIndex<AgentCheckoutIndex>(index => index.PlatformId == platformId && !index.IsTerminal, collection: AgenticCommerceStorage.CollectionName)
                .CountAsync(cancellationToken);
        }

        // The start of the id is the origin's: every profile path on that origin has it.
        var origin = platformId[..AgentPlatform.UnregisteredOriginKeyLength];

        return Session.QueryIndex<AgentCheckoutIndex>(index => index.PlatformId.StartsWith(origin) && !index.IsTerminal, collection: AgenticCommerceStorage.CollectionName)
            .CountAsync(cancellationToken);
    }
}
