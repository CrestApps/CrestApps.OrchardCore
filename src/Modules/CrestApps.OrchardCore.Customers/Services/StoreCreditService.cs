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

    /// <summary>
    /// Returns a holder's store credit in a currency, or <see langword="null"/> when they have none.
    /// </summary>
    Task<StoreCreditAccount> FindAsync(StoreCreditHolder holder, string currency, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default YesSql-backed <see cref="IStoreCreditStore"/>.
/// </summary>
public sealed class StoreCreditStore : DocumentCatalog<StoreCreditAccount, StoreCreditAccountIndex>, IStoreCreditStore
{
    protected override bool CheckConcurrency => true;

    public StoreCreditStore(ISession session, IClock clock)
        : base(session, clock)
    {
        CollectionName = CustomersConstants.CollectionName;
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

    public Task<StoreCreditAccount> FindAsync(StoreCreditHolder holder, string currency, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(holder.CustomerId))
        {
            return FindAsync(holder.CustomerId, currency, cancellationToken);
        }

        currency = currency?.Trim().ToUpperInvariant();
        var kind = holder.Party.Kind;
        var id = holder.Party.Id;

        return holder.Party.IsEmpty || string.IsNullOrEmpty(currency)
            ? Task.FromResult<StoreCreditAccount>(null)
            : Session.Query<StoreCreditAccount, StoreCreditAccountIndex>(index => index.PartyKind == kind && index.PartyId == id && index.Currency == currency, collection: CollectionName)
                .FirstOrDefaultAsync(cancellationToken);
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

    /// <summary>
    /// Adds <paramref name="amount"/> to the customer's store credit in <paramref name="currency"/>, or spends it when it's
    /// negative, only when the credit covers it, and only once for a <paramref name="reference"/>: making the same change
    /// again finds it already made. The change is saved before it returns.
    /// </summary>
    /// <param name="customerId">The customer record's id.</param>
    /// <param name="currency">The ISO 4217 currency code.</param>
    /// <param name="amount">The amount to add, or a negative amount to spend.</param>
    /// <param name="reason">Why the credit changes.</param>
    /// <param name="actor">Who makes the change, if a person does.</param>
    /// <param name="reference">What the change is made for, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<CustomerCreditResult> TryChangeAsync(string customerId, string currency, decimal amount, string reason, string actor, string reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(customerId);

        return TryChangeAsync(StoreCreditHolder.ForCustomer(customerId), currency, amount, reason, actor, reference, cancellationToken);
    }

    /// <summary>
    /// Changes the store credit of a holder, a customer record or another party such as a business account, as
    /// <see cref="TryChangeAsync(string, string, decimal, string, string, string, CancellationToken)"/> does.
    /// </summary>
    /// <param name="holder">Who the credit belongs to.</param>
    /// <param name="currency">The ISO 4217 currency code.</param>
    /// <param name="amount">The amount to add, or a negative amount to spend.</param>
    /// <param name="reason">Why the credit changes.</param>
    /// <param name="actor">Who makes the change, if a person does.</param>
    /// <param name="reference">What the change is made for, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<CustomerCreditResult> TryChangeAsync(StoreCreditHolder holder, string currency, decimal amount, string reason, string actor, string reference, CancellationToken cancellationToken = default)
    {
        if (holder.IsEmpty)
        {
            throw new ArgumentException("The credit must belong to a customer or a party.", nameof(holder));
        }

        if (amount == 0m || string.IsNullOrWhiteSpace(currency))
        {
            return CustomerCreditResult.Refused(CustomerCreditError.InvalidAmount);
        }

        currency = currency.Trim().ToUpperInvariant();

        var account = await _store.FindAsync(holder, currency, cancellationToken);

        if (account is null)
        {
            if (amount < 0m)
            {
                return CustomerCreditResult.Refused(CustomerCreditError.InsufficientBalance);
            }

            // A first credit makes the account, under the customer's lock, so two credits at once make one account.
            var (creating, created) = await _distributedLock.TryAcquireLockAsync(CheckoutTenderLocks.GetKey(TenderKey, "new-" + holder.LockKey), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));

            if (!created)
            {
                return CustomerCreditResult.Refused(CustomerCreditError.Busy);
            }

            await using (creating)
            {
                account = await _store.FindAsync(holder, currency, cancellationToken);

                if (account is null)
                {
                    account = new StoreCreditAccount
                    {
                        ItemId = IdGenerator.GenerateId(),
                        CustomerId = holder.CustomerId,
                        PartyKind = string.IsNullOrEmpty(holder.CustomerId) ? holder.Party.Kind : null,
                        PartyId = string.IsNullOrEmpty(holder.CustomerId) ? holder.Party.Id : null,
                        Currency = currency,
                    };

                    Change(account, amount, reason, actor, reference);

                    await _store.CreateAsync(account, cancellationToken);
                    await _session.SaveChangesAsync(cancellationToken);

                    return new CustomerCreditResult { Succeeded = true, Balance = account.Balance };
                }
            }
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(CheckoutTenderLocks.GetKey(TenderKey, account.ItemId), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));

        if (!locked)
        {
            return CustomerCreditResult.Refused(CustomerCreditError.Busy, account.Balance);
        }

        await using (locker)
        {
            account = await _store.FindByIdAsync(account.ItemId, cancellationToken);

            if (!string.IsNullOrEmpty(reference) &&
                (account.Activity ?? []).Any(entry => string.Equals(entry.Reference, reference, StringComparison.Ordinal)))
            {
                return new CustomerCreditResult { Succeeded = true, AlreadyApplied = true, Balance = account.Balance };
            }

            if (amount < 0m && account.Balance < -amount)
            {
                return CustomerCreditResult.Refused(CustomerCreditError.InsufficientBalance, account.Balance);
            }

            Change(account, amount, reason, actor, reference);

            await _store.UpdateAsync(account, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return new CustomerCreditResult { Succeeded = true, Balance = account.Balance };
        }
    }

    private void Change(StoreCreditAccount account, decimal amount, string reason, string actor, string reference)
    {
        account.ChangeBalance(amount, reason, _clock.UtcNow, actor: actor);
        account.Activity[^1].Reference = reference;
    }
}
