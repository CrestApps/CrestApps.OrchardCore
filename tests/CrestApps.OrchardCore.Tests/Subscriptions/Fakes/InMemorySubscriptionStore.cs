using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;

namespace CrestApps.OrchardCore.Tests.Subscriptions.Fakes;

/// <summary>
/// An in-memory <see cref="ISubscriptionStore"/> so lifecycle behavior can be tested without a database.
/// </summary>
internal sealed class InMemorySubscriptionStore : ISubscriptionStore
{
    private readonly Dictionary<string, Subscription> _subscriptions = new(StringComparer.Ordinal);

    public InMemorySubscriptionStore(params Subscription[] seed)
    {
        foreach (var subscription in seed)
        {
            if (string.IsNullOrEmpty(subscription.ItemId))
            {
                subscription.ItemId = UniqueId.GenerateId();
            }

            _subscriptions[subscription.ItemId] = subscription;
        }
    }

    public IReadOnlyCollection<Subscription> Subscriptions
        => _subscriptions.Values;

    public ValueTask CreateAsync(Subscription entry, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(entry.ItemId))
        {
            entry.ItemId = UniqueId.GenerateId();
        }

        _subscriptions[entry.ItemId] = entry;

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(Subscription entry, CancellationToken cancellationToken = default)
    {
        _subscriptions[entry.ItemId] = entry;

        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> DeleteAsync(Subscription entry, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_subscriptions.Remove(entry.ItemId));

    public ValueTask<Subscription> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_subscriptions.GetValueOrDefault(id ?? string.Empty));

    public ValueTask<IReadOnlyCollection<Subscription>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<Subscription>>([.. _subscriptions.Values.Where(s => ids.Contains(s.ItemId, StringComparer.Ordinal))]);

    public ValueTask<IReadOnlyCollection<Subscription>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<Subscription>>([.. _subscriptions.Values]);

    public ValueTask<PageResult<Subscription>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
        where TQuery : QueryContext
    {
        var entries = _subscriptions.Values.ToArray();

        return ValueTask.FromResult(new PageResult<Subscription>
        {
            Count = entries.Length,
            Entries = entries,
        });
    }

    public Task<Subscription> GetByProviderSubscriptionIdAsync(string providerSubscriptionId, CancellationToken cancellationToken = default)
        => Task.FromResult(_subscriptions.Values.FirstOrDefault(s => s.ProviderSubscriptionId == providerSubscriptionId));

    public Task<Subscription> GetByObligationAsync(string checkoutSessionId, string obligationId, CancellationToken cancellationToken = default)
        => Task.FromResult(_subscriptions.Values.FirstOrDefault(s =>
            string.Equals(s.CheckoutSessionId, checkoutSessionId, StringComparison.Ordinal) &&
            string.Equals(s.ObligationId, obligationId, StringComparison.Ordinal)));

    public Task<IReadOnlyList<Subscription>> GetByCheckoutSessionAsync(string checkoutSessionId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Subscription>>([.. _subscriptions.Values.Where(s => s.CheckoutSessionId == checkoutSessionId)]);

    public Task<IReadOnlyList<Subscription>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Subscription>>([.. _subscriptions.Values.Where(s => s.OwnerId == ownerId)]);

    public Task<IReadOnlyList<Subscription>> GetDueForRenewalAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Subscription>>(
            [.. _subscriptions.Values.Where(s =>
                s.NextBillingUtc.HasValue &&
                s.NextBillingUtc.Value <= asOfUtc &&
                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))]);

    public Task<IReadOnlyList<Subscription>> GetLapsedAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Subscription>>(
            [.. _subscriptions.Values.Where(s =>
                s.Status == SubscriptionStatus.PastDue &&
                s.GraceEndsUtc.HasValue &&
                s.GraceEndsUtc.Value <= asOfUtc)]);

    public Task<PageResult<Subscription>> PageAsync(int page, int pageSize, SubscriptionQuery query, CancellationToken cancellationToken = default)
    {
        var matches = _subscriptions.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(query?.OwnerId))
        {
            matches = matches.Where(s => s.OwnerId == query.OwnerId);
        }

        if (query?.Status is not null)
        {
            matches = matches.Where(s => s.Status == query.Status.Value);
        }

        var all = matches.ToArray();

        return Task.FromResult(new PageResult<Subscription>
        {
            Count = all.Length,
            Entries = [.. all.Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize)],
        });
    }
}
