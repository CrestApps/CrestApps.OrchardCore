using CrestApps.OrchardCore.Payments.Models;

namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// Describes the billing cycle a recurring <see cref="Transaction"/> covers.
/// </summary>
/// <remarks>
/// An offline provider has no gateway keeping the schedule, so the ledger has to carry it. Without this a
/// recurring "pay later" commitment would produce exactly one invoice and then silently stop billing, and a
/// reminder could not tell the customer which period they owe for.
/// </remarks>
public sealed class TransactionRecurrence
{
    /// <summary>
    /// Gets or sets the number of <see cref="DurationType"/> units in one billing cycle.
    /// </summary>
    public int BillingDuration { get; set; }

    /// <summary>
    /// Gets or sets the unit of time <see cref="BillingDuration"/> is expressed in.
    /// </summary>
    public DurationType DurationType { get; set; }

    /// <summary>
    /// Gets or sets the UTC start of the period this transaction covers.
    /// </summary>
    public DateTime PeriodStartUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC end of the period this transaction covers.
    /// </summary>
    public DateTime PeriodEndUtc { get; set; }

    /// <summary>
    /// Gets or sets the one-based number of this cycle, so a renewal can be capped and reported.
    /// </summary>
    public int CycleNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the total number of cycles the customer agreed to, when the agreement is capped.
    /// </summary>
    public int? CycleLimit { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the agreement has been stopped, so no further cycle is
    /// created even though the schedule would allow one.
    /// </summary>
    public bool Canceled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the transaction for the following cycle has already been
    /// created. It is what makes the renewal sweep create each cycle exactly once, even if it runs twice or
    /// on two nodes.
    /// </summary>
    public bool NextCycleCreated { get; set; }

    /// <summary>
    /// Returns the UTC start of the cycle that follows this one, or <see langword="null"/> when the
    /// agreement has ended or the next cycle already exists.
    /// </summary>
    public DateTime? GetNextCycleUtc()
    {
        if (Canceled || NextCycleCreated || BillingDuration <= 0)
        {
            return null;
        }

        if (CycleLimit.HasValue && CycleNumber >= CycleLimit.Value)
        {
            return null;
        }

        return PeriodEndUtc;
    }

    /// <summary>
    /// Advances a UTC date by one billing cycle.
    /// </summary>
    /// <param name="from">The date to advance from.</param>
    public DateTime Advance(DateTime from)
        => DurationType switch
        {
            DurationType.Day => from.AddDays(BillingDuration),
            DurationType.Week => from.AddDays(BillingDuration * 7),
            DurationType.Month => from.AddMonths(BillingDuration),
            DurationType.Year => from.AddYears(BillingDuration),
            _ => from.AddMonths(BillingDuration),
        };
}
