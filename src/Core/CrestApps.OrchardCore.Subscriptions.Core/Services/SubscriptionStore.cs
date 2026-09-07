using CrestApps.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// The default YesSql-backed <see cref="ISubscriptionStore"/>.
/// </summary>
public sealed class SubscriptionStore : DocumentCatalog<Subscription, SubscriptionRecordIndex>, ISubscriptionStore
{
    private readonly IClock _clock;

    /// <summary>
    /// Enables YesSql document-version concurrency checks, so a renewal and a cancellation arriving at the
    /// same moment cannot silently overwrite one another. Losing either would either bill a customer who
    /// canceled or give away a cycle that was paid for.
    /// </summary>
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionStore"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session.</param>
    /// <param name="clock">The clock used for timestamps.</param>
    public SubscriptionStore(ISession session, IClock clock)
        : base(session)
    {
        CollectionName = SubscriptionConstants.SubscriptionCollectionName;
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task<Subscription> GetByProviderSubscriptionIdAsync(string providerSubscriptionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerSubscriptionId);

        return Session
            .Query<Subscription, SubscriptionRecordIndex>(x => x.ProviderSubscriptionId == providerSubscriptionId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Subscription> GetByObligationAsync(string checkoutSessionId, string obligationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(checkoutSessionId);
        ArgumentException.ThrowIfNullOrEmpty(obligationId);

        return Session
            .Query<Subscription, SubscriptionRecordIndex>(
                x => x.CheckoutSessionId == checkoutSessionId && x.ObligationId == obligationId,
                collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Subscription>> GetByCheckoutSessionAsync(string checkoutSessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(checkoutSessionId);

        var records = await Session
            .Query<Subscription, SubscriptionRecordIndex>(x => x.CheckoutSessionId == checkoutSessionId, collection: CollectionName)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Subscription>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);

        var records = await Session
            .Query<Subscription, SubscriptionRecordIndex>(x => x.OwnerId == ownerId, collection: CollectionName)
            .OrderByDescending(x => x.CreatedUtc)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Subscription>> GetDueForRenewalAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        // Trialing is included deliberately: the end of a trial is the first cycle to bill, and skipping it
        // would give the customer the plan for free forever.
        var records = await Session
            .Query<Subscription, SubscriptionRecordIndex>(
                x => x.NextBillingUtc != null &&
                    x.NextBillingUtc <= asOfUtc &&
                    (x.Status == SubscriptionStatus.Active || x.Status == SubscriptionStatus.Trialing),
                collection: CollectionName)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Subscription>> GetLapsedAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        var records = await Session
            .Query<Subscription, SubscriptionRecordIndex>(
                x => x.Status == SubscriptionStatus.PastDue && x.GraceEndsUtc != null && x.GraceEndsUtc <= asOfUtc,
                collection: CollectionName)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }

    /// <inheritdoc/>
    public async Task<PageResult<Subscription>> PageAsync(int page, int pageSize, SubscriptionQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var records = Session.Query<Subscription, SubscriptionRecordIndex>(collection: CollectionName);

        if (!string.IsNullOrEmpty(query.OwnerId))
        {
            records = records.Where(x => x.OwnerId == query.OwnerId);
        }

        if (query.Status.HasValue)
        {
            records = records.Where(x => x.Status == query.Status.Value);
        }

        if (!string.IsNullOrEmpty(query.ProviderKey))
        {
            records = records.Where(x => x.ProviderKey == query.ProviderKey);
        }

        if (!string.IsNullOrEmpty(query.ReferenceType))
        {
            records = records.Where(x => x.ReferenceType == query.ReferenceType);
        }

        if (!string.IsNullOrEmpty(query.ReferenceId))
        {
            records = records.Where(x => x.ReferenceId == query.ReferenceId);
        }

        if (!string.IsNullOrEmpty(query.Search))
        {
            records = records.Where(x => x.Title.Contains(query.Search));
        }

        var count = await records.CountAsync(cancellationToken);

        var entries = await records
            .OrderByDescending(x => x.CreatedUtc)
            .ThenByDescending(x => x.ItemId)
            .Skip((Math.Max(page, 1) - 1) * pageSize)
            .Take(pageSize)
            .ListAsync(cancellationToken);

        return new PageResult<Subscription>
        {
            Count = count,
            Entries = entries.ToArray(),
        };
    }

    /// <inheritdoc/>
    protected override ValueTask SavingAsync(Subscription record)
    {
        var now = _clock.UtcNow;

        if (record.CreatedUtc == default)
        {
            record.CreatedUtc = now;
        }

        record.UpdatedUtc = now;

        return ValueTask.CompletedTask;
    }
}
