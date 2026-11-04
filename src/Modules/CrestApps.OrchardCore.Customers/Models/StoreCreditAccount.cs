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
    /// Gets or sets the customer the credit belongs to.
    /// </summary>
    public string CustomerId { get; set; }

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
}
