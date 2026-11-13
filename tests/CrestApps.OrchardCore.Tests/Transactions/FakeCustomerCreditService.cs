using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// An in-memory <see cref="ICustomerCreditService"/> with the same rules as store credit: users and customer records hold
/// credit, a change is made once per reference, and spending never takes more than there is.
/// </summary>
internal sealed class FakeCustomerCreditService : ICustomerCreditService
{
    private readonly Dictionary<(PartyRef Party, string Currency), decimal> _balances = [];

    public List<CustomerCreditChange> Changes { get; } = [];

    public void Give(PartyRef party, string currency, decimal amount)
        => _balances[(party, currency)] = Balance(party, currency) + amount;

    public decimal Balance(PartyRef party, string currency)
        => _balances.GetValueOrDefault((party, currency));

    public bool CanHold(PartyRef party)
        => party.Kind is PartyKinds.User or PartyKinds.Customer;

    public Task<decimal> GetBalanceAsync(PartyRef party, string currency, CancellationToken cancellationToken = default)
        => Task.FromResult(Balance(party, currency));

    public Task<CustomerCreditResult> AddAsync(CustomerCreditChange change, CancellationToken cancellationToken = default)
        => Task.FromResult(Change(change, change.Amount));

    public Task<CustomerCreditResult> SpendAsync(CustomerCreditChange change, CancellationToken cancellationToken = default)
        => Task.FromResult(Change(change, -change.Amount));

    private CustomerCreditResult Change(CustomerCreditChange change, decimal amount)
    {
        if (!CanHold(change.Party))
        {
            return CustomerCreditResult.Refused(CustomerCreditError.UnsupportedParty);
        }

        var balance = Balance(change.Party, change.Currency);

        if (change.Reference is not null && Changes.Any(made => made.Reference == change.Reference))
        {
            return new CustomerCreditResult { Succeeded = true, AlreadyApplied = true, Balance = balance };
        }

        if (balance + amount < 0m)
        {
            return CustomerCreditResult.Refused(CustomerCreditError.InsufficientBalance, balance);
        }

        _balances[(change.Party, change.Currency)] = balance + amount;
        Changes.Add(change);

        return new CustomerCreditResult { Succeeded = true, Balance = balance + amount };
    }
}
