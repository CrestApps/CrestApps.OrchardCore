using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Indexes;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// Persists customers' store credit.
/// </summary>
public interface IStoreCreditStore : ICatalog<StoreCreditAccount>
{
    /// <summary>
    /// Returns the customer's store credit in a currency, or <see langword="null"/> when they have none.
    /// </summary>
    Task<StoreCreditAccount> FindAsync(string customerId, string currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every currency of store credit the customer holds.
    /// </summary>
    Task<IReadOnlyList<StoreCreditAccount>> ListByCustomerAsync(string customerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default YesSql-backed <see cref="IStoreCreditStore"/>.
/// </summary>
public sealed class StoreCreditStore : DocumentCatalog<StoreCreditAccount, StoreCreditAccountIndex>, IStoreCreditStore
{
    private readonly IClock _clock;

    protected override bool CheckConcurrency => true;

    public StoreCreditStore(ISession session, IClock clock)
        : base(session)
    {
        CollectionName = CustomersConstants.CollectionName;
        _clock = clock;
    }

    public Task<StoreCreditAccount> FindAsync(string customerId, string currency, CancellationToken cancellationToken = default)
    {
        currency = currency?.Trim().ToUpperInvariant();

        return string.IsNullOrEmpty(customerId) || string.IsNullOrEmpty(currency)
            ? Task.FromResult<StoreCreditAccount>(null)
            : Session.Query<StoreCreditAccount, StoreCreditAccountIndex>(index => index.CustomerId == customerId && index.Currency == currency, collection: CollectionName)
                .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StoreCreditAccount>> ListByCustomerAsync(string customerId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(customerId)
            ? []
            : [.. await Session.Query<StoreCreditAccount, StoreCreditAccountIndex>(index => index.CustomerId == customerId, collection: CollectionName).ListAsync(cancellationToken)];

    protected override ValueTask SavingAsync(StoreCreditAccount record)
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

/// <summary>
/// Gives customers store credit and changes it by hand, under the lock the checkout takes when it spends it.
/// </summary>
public sealed class StoreCreditService
{
    /// <summary>
    /// The key store credit pays toward checkouts with (<see cref="ICheckoutTenderProvider.Key"/>).
    /// </summary>
    public const string TenderKey = "store-credit";

    private readonly IStoreCreditStore _store;
    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;

    public StoreCreditService(
        IStoreCreditStore store,
        ISession session,
        IDistributedLock distributedLock,
        IClock clock)
    {
        _store = store;
        _session = session;
        _distributedLock = distributedLock;
        _clock = clock;
    }

    /// <summary>
    /// Adds <paramref name="amount"/> to the customer's store credit in <paramref name="currency"/>, or takes it off
    /// when it's negative. Returns the account, or <see langword="null"/> when its lock couldn't be taken.
    /// </summary>
    public async Task<StoreCreditAccount> ChangeAsync(string customerId, string currency, decimal amount, string reason, string actor, string sessionId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(customerId);
        ArgumentException.ThrowIfNullOrEmpty(currency);

        currency = currency.Trim().ToUpperInvariant();

        var account = await _store.FindAsync(customerId, currency, cancellationToken);

        if (account is null)
        {
            if (amount <= 0m)
            {
                return null;
            }

            // A first credit makes the account, under the customer's lock, so two credits at once make one account.
            var (creating, created) = await _distributedLock.TryAcquireLockAsync(CheckoutTenderLocks.GetKey(TenderKey, "new-" + customerId), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));

            if (!created)
            {
                return null;
            }

            await using (creating)
            {
                account = await _store.FindAsync(customerId, currency, cancellationToken);

                if (account is null)
                {
                    account = new StoreCreditAccount
                    {
                        ItemId = IdGenerator.GenerateId(),
                        CustomerId = customerId,
                        Currency = currency,
                    };

                    account.ChangeBalance(amount, reason, _clock.UtcNow, sessionId, actor);

                    await _store.CreateAsync(account, cancellationToken);
                    await _session.SaveChangesAsync(cancellationToken);

                    return account;
                }
            }
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(CheckoutTenderLocks.GetKey(TenderKey, account.ItemId), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));

        if (!locked)
        {
            return null;
        }

        await using (locker)
        {
            account = await _store.FindByIdAsync(account.ItemId, cancellationToken);
            account.ChangeBalance(amount, reason, _clock.UtcNow, sessionId, actor);

            await _store.UpdateAsync(account, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return account;
        }
    }
}
