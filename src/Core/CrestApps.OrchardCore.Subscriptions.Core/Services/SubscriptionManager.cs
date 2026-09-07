using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// The default <see cref="ISubscriptionManager"/>. It delegates storage to <see cref="ISubscriptionStore"/>
/// and runs loaded agreements through the registered catalog handlers.
/// </summary>
public sealed class SubscriptionManager : CatalogManager<Subscription>, ISubscriptionManager
{
    private readonly ISubscriptionStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionManager"/> class.
    /// </summary>
    /// <param name="store">The underlying subscription store.</param>
    /// <param name="handlers">The catalog entry handlers.</param>
    /// <param name="logger">The logger.</param>
    public SubscriptionManager(
        ISubscriptionStore store,
        IEnumerable<ICatalogEntryHandler<Subscription>> handlers,
        ILogger<CatalogManager<Subscription>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<Subscription> GetByProviderSubscriptionIdAsync(string providerSubscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await _store.GetByProviderSubscriptionIdAsync(providerSubscriptionId, cancellationToken);

        if (subscription is not null)
        {
            await LoadAsync(subscription, cancellationToken);
        }

        return subscription;
    }

    /// <inheritdoc/>
    public async Task<Subscription> GetByObligationAsync(string checkoutSessionId, string obligationId, CancellationToken cancellationToken = default)
    {
        var subscription = await _store.GetByObligationAsync(checkoutSessionId, obligationId, cancellationToken);

        if (subscription is not null)
        {
            await LoadAsync(subscription, cancellationToken);
        }

        return subscription;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Subscription>> GetByCheckoutSessionAsync(string checkoutSessionId, CancellationToken cancellationToken = default)
        => await LoadAllAsync(await _store.GetByCheckoutSessionAsync(checkoutSessionId, cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Subscription>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
        => await LoadAllAsync(await _store.GetByOwnerAsync(ownerId, cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Subscription>> GetDueForRenewalAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
        => await LoadAllAsync(await _store.GetDueForRenewalAsync(asOfUtc, cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Subscription>> GetLapsedAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
        => await LoadAllAsync(await _store.GetLapsedAsync(asOfUtc, cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public async Task<PageResult<Subscription>> PageAsync(int page, int pageSize, SubscriptionQuery query, CancellationToken cancellationToken = default)
    {
        var result = await _store.PageAsync(page, pageSize, query, cancellationToken);

        foreach (var entry in result.Entries)
        {
            await LoadAsync(entry, cancellationToken);
        }

        return result;
    }

    private async Task<IReadOnlyList<Subscription>> LoadAllAsync(IReadOnlyList<Subscription> subscriptions, CancellationToken cancellationToken)
    {
        foreach (var subscription in subscriptions)
        {
            await LoadAsync(subscription, cancellationToken);
        }

        return subscriptions;
    }
}
