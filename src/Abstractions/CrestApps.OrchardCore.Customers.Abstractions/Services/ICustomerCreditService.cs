using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// A party's customer credit: money kept for them to spend later, such as what they paid above a balance, or a credit
/// note on an invoice they had already paid. The Customers <b>Store Credit</b> feature implements it over each customer's
/// store credit balance, so the credit the ledger keeps is the same one the customer spends at checkout. Transactions
/// resolves it when it is registered, and offers nothing to do with credit when it is not.
/// </summary>
/// <remarks>
/// Every party a debt can have holds credit except a guest: a user (<see cref="PartyKinds.User"/>) and a customer record
/// (<see cref="PartyKinds.Customer"/>) on the customer record, and any other party, such as a business account
/// (<see cref="PartyKinds.Account"/>), on store credit of its own. A guest is refused with
/// <see cref="CustomerCreditError.UnsupportedParty"/>. Each change takes the credit's lock and is saved before it returns.
/// </remarks>
public interface ICustomerCreditService
{
    /// <summary>
    /// Returns whether a party can hold customer credit.
    /// </summary>
    /// <param name="party">The party.</param>
    bool CanHold(PartyRef party);

    /// <summary>
    /// Returns whether a signed-in user may spend a party's credit themselves, such as toward the party's invoices: their
    /// own, or a party they have <see cref="PartyAccess.Billing"/> for (an account's admins, never a plain member). Staff who
    /// manage transactions apply credit through their permission instead.
    /// </summary>
    /// <param name="party">The party whose credit is spent.</param>
    /// <param name="userId">The user's id.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<bool> CanSpendAsync(PartyRef party, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a party's credit in a currency, or zero when they have none.
    /// </summary>
    /// <param name="party">The party.</param>
    /// <param name="currency">The ISO 4217 currency code.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<decimal> GetBalanceAsync(PartyRef party, string currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds money to a party's credit.
    /// </summary>
    /// <param name="change">The change.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<CustomerCreditResult> AddAsync(CustomerCreditChange change, CancellationToken cancellationToken = default);

    /// <summary>
    /// Spends money from a party's credit. It is refused, and nothing is spent, when the credit is less than the amount.
    /// </summary>
    /// <param name="change">The change.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<CustomerCreditResult> SpendAsync(CustomerCreditChange change, CancellationToken cancellationToken = default);
}
