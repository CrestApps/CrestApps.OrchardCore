using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Customers.Models;

/// <summary>
/// A change to a party's customer credit: money added to it, or spent from it.
/// </summary>
public sealed class CustomerCreditChange
{
    /// <summary>
    /// Gets or sets the party whose credit changes, such as the payer of a transaction.
    /// </summary>
    public PartyRef Party { get; set; }

    /// <summary>
    /// Gets or sets the ISO 4217 currency code of the credit.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the amount, greater than zero, in the credit's currency.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets why the credit changes, shown on the credit's activity.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets what the change is made for, such as a transaction's payment. A change with a reference is made only
    /// once: making it again returns the change already made.
    /// </summary>
    public string Reference { get; set; }

    /// <summary>
    /// Gets or sets who makes the change, if a person does.
    /// </summary>
    public string Actor { get; set; }
}
