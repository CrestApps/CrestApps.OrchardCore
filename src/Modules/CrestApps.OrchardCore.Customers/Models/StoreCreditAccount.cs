using CrestApps.Core.Models;
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
public sealed class StoreCreditAccount : CatalogItem
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
        var before = Balance;

        Balance = Money.Round(Math.Max(0m, Balance + amount), Currency);
        Activity ??= [];
        Activity.Add(new StoreCreditActivity
        {
            CreatedUtc = utcNow,
            Amount = Balance - before,
            Balance = Balance,
            Reason = reason,
            SessionId = sessionId,
            Actor = actor,
        });
    }
}

/// <summary>
/// One change to a customer's store credit.
/// </summary>
public sealed class StoreCreditActivity
{
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets how much it changed: positive when credit was given, negative when it was spent.
    /// </summary>
    public decimal Amount { get; set; }

    public decimal Balance { get; set; }

    public string Reason { get; set; }

    public string SessionId { get; set; }

    public string Actor { get; set; }
}
