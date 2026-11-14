using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// The <see cref="ICustomerCreditService"/> of the Store Credit feature: a party's customer credit is store credit, the
/// same balance a customer spends at checkout. A user's and a customer record's credit is kept on the customer record (a
/// user's record is made the first time they are given credit); any other party, such as a business account, has store
/// credit of its own, keyed by the party.
/// </summary>
public sealed class StoreCreditCustomerCreditService : ICustomerCreditService
{
    private readonly StoreCreditService _storeCredit;
    private readonly IStoreCreditStore _store;
    private readonly ICustomerStore _customerStore;
    private readonly ICustomerService _customerService;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreCreditCustomerCreditService"/> class.
    /// </summary>
    /// <param name="storeCredit">The service that changes store credit under its lock.</param>
    /// <param name="store">The store credit accounts.</param>
    /// <param name="customerStore">The customer records, to find a user's.</param>
    /// <param name="customerService">The customer service, to make a user's record the first time they get credit.</param>
    /// <param name="serviceProvider">The service provider the party membership service comes from, when it is registered.</param>
    public StoreCreditCustomerCreditService(
        StoreCreditService storeCredit,
        IStoreCreditStore store,
        ICustomerStore customerStore,
        ICustomerService customerService,
        IServiceProvider serviceProvider)
    {
        _storeCredit = storeCredit;
        _store = store;
        _customerStore = customerStore;
        _customerService = customerService;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public bool CanHold(PartyRef party)
        => !party.IsEmpty && !string.Equals(party.Kind, PartyKinds.Guest, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<bool> CanSpendAsync(PartyRef party, string userId, CancellationToken cancellationToken = default)
    {
        if (!CanHold(party) || string.IsNullOrEmpty(userId))
        {
            return false;
        }

        if (string.Equals(party.Kind, PartyKinds.User, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(party.Id, userId, StringComparison.Ordinal);
        }

        // Spending credit settles the party's money, which takes billing access: an account's admins, never a plain buyer.
        return _serviceProvider.GetService<IPartyMembershipService>() is { } membership &&
            await membership.IsMemberAsync(party, userId, PartyAccess.Billing, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<decimal> GetBalanceAsync(PartyRef party, string currency, CancellationToken cancellationToken = default)
    {
        var holder = await FindHolderAsync(party, create: false, cancellationToken);

        if (holder.IsEmpty || string.IsNullOrWhiteSpace(currency))
        {
            return 0m;
        }

        return (await _store.FindAsync(holder, currency, cancellationToken))?.Balance ?? 0m;
    }

    /// <inheritdoc/>
    public async Task<CustomerCreditResult> AddAsync(CustomerCreditChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (change.Amount <= 0m)
        {
            return CustomerCreditResult.Refused(CustomerCreditError.InvalidAmount);
        }

        var holder = await FindHolderAsync(change.Party, create: true, cancellationToken);

        return holder.IsEmpty
            ? CustomerCreditResult.Refused(CustomerCreditError.UnsupportedParty)
            : await _storeCredit.TryChangeAsync(holder, change.Currency, change.Amount, change.Reason, change.Actor, change.Reference, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CustomerCreditResult> SpendAsync(CustomerCreditChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (change.Amount <= 0m)
        {
            return CustomerCreditResult.Refused(CustomerCreditError.InvalidAmount);
        }

        if (!CanHold(change.Party))
        {
            return CustomerCreditResult.Refused(CustomerCreditError.UnsupportedParty);
        }

        var holder = await FindHolderAsync(change.Party, create: false, cancellationToken);

        return holder.IsEmpty
            ? CustomerCreditResult.Refused(CustomerCreditError.InsufficientBalance)
            : await _storeCredit.TryChangeAsync(holder, change.Currency, -change.Amount, change.Reason, change.Actor, change.Reference, cancellationToken);
    }

    // Whose store credit is the party's: a customer record's own, a user's customer record (made on the first credit, so
    // money paid above a balance always has somewhere to go), or the party's own account for any other party.
    private async Task<StoreCreditHolder> FindHolderAsync(PartyRef party, bool create, CancellationToken cancellationToken)
    {
        if (!CanHold(party))
        {
            return default;
        }

        if (string.Equals(party.Kind, PartyKinds.Customer, StringComparison.OrdinalIgnoreCase))
        {
            return await _customerStore.FindByIdAsync(party.Id, cancellationToken) is { } record
                ? StoreCreditHolder.ForCustomer(record.ItemId)
                : default;
        }

        if (string.Equals(party.Kind, PartyKinds.User, StringComparison.OrdinalIgnoreCase))
        {
            var customer = create
                ? await _customerService.GetOrCreateForUserAsync(party.Id, cancellationToken)
                : await _customerStore.FindByUserIdAsync(party.Id, cancellationToken);

            return customer is null ? default : StoreCreditHolder.ForCustomer(customer.ItemId);
        }

        return StoreCreditHolder.ForParty(party);
    }
}
