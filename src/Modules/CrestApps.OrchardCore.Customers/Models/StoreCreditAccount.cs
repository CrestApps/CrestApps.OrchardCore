using CrestApps.Core.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Customers.Models;

/// <summary>
/// The store credit a customer holds in one currency: money the store owes them, such as a refund given as credit,
/// that pays toward their checkouts.
/// </summary>
/// <remarks>
/// Its <see cref="Balance"/> only changes under the account's lock, through the checkout's tender ledger or a staff
/// change, and every change is written to <see cref="Activity"/>.
/// </remarks>
public sealed class StoreCreditAccount : CatalogItem, ITimestampedModel
{
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the customer the credit belongs to, or <see langword="null"/> when it belongs to another party, such as
    /// a business account (<see cref="PartyKind"/> and <see cref="PartyId"/>).
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the kind of the party the credit belongs to when it is not a customer record, such as <c>Account</c>.
    /// Accounts kept for a customer leave it empty, as they always have.
    /// </summary>
    public string PartyKind { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the party named by <see cref="PartyKind"/>.
    /// </summary>
    public string PartyId { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency it holds. It pays only toward checkouts in that currency.
    /// </summary>
    public string Currency { get; set; }

    public decimal Balance { get; set; }

    /// <summary>
    /// Gets or sets every change to the balance, oldest first.
    /// </summary>
    public List<StoreCreditActivity> Activity { get; set; } = [];

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Changes the balance by <paramref name="amount"/>, never below zero, and records why.
    /// </summary>
    public void ChangeBalance(decimal amount, string reason, DateTime utcNow, string sessionId = null, string actor = null)
    {
        Activity ??= [];
        Balance = BalanceLedger.ChangeBalance(Balance, amount, Currency, Activity, reason, utcNow, sessionId, actor);
    }
}

/// <summary>
/// One change to a customer's store credit.
/// </summary>
public sealed class StoreCreditActivity : BalanceLedgerEntry
{
    /// <summary>
    /// Gets or sets what the change was made for, such as a transaction's payment, if anything. A change with a
    /// reference is made only once: making it again finds this entry (<see cref="Services.StoreCreditService.TryChangeAsync"/>).
    /// </summary>
    public string Reference { get; set; }
}
