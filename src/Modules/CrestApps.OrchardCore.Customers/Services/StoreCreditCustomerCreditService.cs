using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// The <see cref="ICustomerCreditService"/> of the Store Credit feature: a party's customer credit is the store credit of
/// their customer record, the same balance they spend at checkout. A user's credit is kept on their customer record, which
/// is made the first time they are given credit.
/// </summary>
public sealed class StoreCreditCustomerCreditService : ICustomerCreditService
{
    private readonly StoreCreditService _storeCredit;
    private readonly IStoreCreditStore _store;
    private readonly ICustomerStore _customerStore;
    private readonly ICustomerService _customerService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreCreditCustomerCreditService"/> class.
    /// </summary>
    /// <param name="storeCredit">The service that changes store credit under its lock.</param>
    /// <param name="store">The store credit accounts.</param>
    /// <param name="customerStore">The customer records, to find a user's.</param>
    /// <param name="customerService">The customer service, to make a user's record the first time they get credit.</param>
    public StoreCreditCustomerCreditService(
        StoreCreditService storeCredit,
        IStoreCreditStore store,
        ICustomerStore customerStore,
        ICustomerService customerService)
    {
        _storeCredit = storeCredit;
        _store = store;
        _customerStore = customerStore;
        _customerService = customerService;
    }

    /// <inheritdoc/>
    public bool CanHold(PartyRef party)
        => !party.IsEmpty &&
            (string.Equals(party.Kind, PartyKinds.User, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(party.Kind, PartyKinds.Customer, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc/>
    public async Task<decimal> GetBalanceAsync(PartyRef party, string currency, CancellationToken cancellationToken = default)
    {
        var customerId = await FindCustomerIdAsync(party, create: false, cancellationToken);

        if (customerId is null || string.IsNullOrWhiteSpace(currency))
        {
            return 0m;
        }

        return (await _store.FindAsync(customerId, currency, cancellationToken))?.Balance ?? 0m;
    }

    /// <inheritdoc/>
    public async Task<CustomerCreditResult> AddAsync(CustomerCreditChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (change.Amount <= 0m)
        {
            return CustomerCreditResult.Refused(CustomerCreditError.InvalidAmount);
        }

        var customerId = await FindCustomerIdAsync(change.Party, create: true, cancellationToken);

        return customerId is null
            ? CustomerCreditResult.Refused(CustomerCreditError.UnsupportedParty)
            : await _storeCredit.TryChangeAsync(customerId, change.Currency, change.Amount, change.Reason, change.Actor, change.Reference, cancellationToken);
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

        var customerId = await FindCustomerIdAsync(change.Party, create: false, cancellationToken);

        return customerId is null
            ? CustomerCreditResult.Refused(CustomerCreditError.InsufficientBalance)
            : await _storeCredit.TryChangeAsync(customerId, change.Currency, -change.Amount, change.Reason, change.Actor, change.Reference, cancellationToken);
    }

    // The customer record whose store credit is the party's: the record itself, or a user's record (made on the first
    // credit, so money paid above a balance always has somewhere to go).
    private async Task<string> FindCustomerIdAsync(PartyRef party, bool create, CancellationToken cancellationToken)
    {
        if (!CanHold(party))
        {
            return null;
        }

        if (string.Equals(party.Kind, PartyKinds.Customer, StringComparison.OrdinalIgnoreCase))
        {
            return (await _customerStore.FindByIdAsync(party.Id, cancellationToken))?.ItemId;
        }

        var customer = create
            ? await _customerService.GetOrCreateForUserAsync(party.Id, cancellationToken)
            : await _customerStore.FindByUserIdAsync(party.Id, cancellationToken);

        return customer?.ItemId;
    }
}
