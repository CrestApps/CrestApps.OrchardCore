using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// How late fees are charged on an overdue invoice: each fee is its own debit note linked to the invoice, never a line
/// added to it. A fee is charged once the grace days after the due date have passed, and again every so many days when
/// it repeats, up to a number of fees and a cap, while what is due is at least the minimum balance.
/// </summary>
public sealed class LateFeePolicy : CatalogItem, INameAwareModel
{
    /// <summary>
    /// Gets or sets the name shown to staff, such as <c>1.5% a month</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a note for staff about when the policy applies.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the policy charges fees.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a fee is a fixed amount or a percent of what is due.
    /// </summary>
    public LateFeeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the fixed amount of each fee, in the invoice's currency.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the percent of what is still due each fee charges.
    /// </summary>
    public decimal Percent { get; set; }

    /// <summary>
    /// Gets or sets how many days after the due date pass before the first fee.
    /// </summary>
    public int GraceDays { get; set; }

    /// <summary>
    /// Gets or sets how many days after a fee the next one is charged; zero or less charges one fee only.
    /// </summary>
    public int RepeatEveryDays { get; set; }

    /// <summary>
    /// Gets or sets the most fees charged on one invoice, or <see langword="null"/> for no limit while it repeats.
    /// </summary>
    public int? MaxFees { get; set; }

    /// <summary>
    /// Gets or sets the most charged in fees on one invoice, in its currency, or <see langword="null"/> for no cap.
    /// </summary>
    public decimal? CapAmount { get; set; }

    /// <summary>
    /// Gets or sets the least that must still be due for a fee to be charged.
    /// </summary>
    public decimal MinimumBalance { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the fee is taxed like a sale; otherwise it is out of scope of tax.
    /// </summary>
    public bool Taxable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether each debit note is e-mailed to the customer once issued.
    /// </summary>
    public bool SendDebitNote { get; set; }
}

/// <summary>
/// How a late fee is worked out.
/// </summary>
public enum LateFeeKind
{
    /// <summary>
    /// A fixed amount.
    /// </summary>
    Flat = 0,

    /// <summary>
    /// A percent of what is still due.
    /// </summary>
    Percent = 1,
}
