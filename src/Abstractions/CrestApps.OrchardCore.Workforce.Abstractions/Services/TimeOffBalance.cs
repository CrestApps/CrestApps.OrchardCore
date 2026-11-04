using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A worker's balance under one policy on a day.
/// </summary>
public sealed class TimeOffBalance
{
    /// <summary>
    /// Gets or sets the policy.
    /// </summary>
    public TimeOffPolicy Policy { get; set; }

    /// <summary>
    /// Gets or sets the policy's type.
    /// </summary>
    public TimeOffType Type { get; set; }

    /// <summary>
    /// Gets or sets the day the balance is for.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the sum of the ledger up to the day.
    /// </summary>
    public decimal Current { get; set; }

    /// <summary>
    /// Gets or sets what the policy will add between today and the day, not yet in the ledger.
    /// </summary>
    public decimal ProjectedAccrual { get; set; }

    /// <summary>
    /// Gets or sets approved time off after the day.
    /// </summary>
    public decimal Upcoming { get; set; }

    /// <summary>
    /// Gets or sets requests that wait for a place or a decision.
    /// </summary>
    public decimal Pending { get; set; }

    /// <summary>
    /// Gets what can still be requested on the day: the current balance and what will accrue, less approved time off
    /// after it and the requests still open.
    /// </summary>
    public decimal Available => Current + ProjectedAccrual - Upcoming - Pending - Expiring;

    /// <summary>
    /// Gets or sets what is left of grants whose last day comes before the day, which will have expired by then.
    /// </summary>
    public decimal Expiring { get; set; }

    /// <summary>
    /// Gets or sets the grants that can still be used on the day, with what is left of each, soonest to expire first.
    /// </summary>
    public IReadOnlyList<TimeOffGrantBalance> Grants { get; set; } = [];
}

/// <summary>
/// What is left of a grant.
/// </summary>
public sealed class TimeOffGrantBalance
{
    /// <summary>
    /// Gets or sets the grant's ledger entry.
    /// </summary>
    public string EntryId { get; set; }

    /// <summary>
    /// Gets or sets how much was granted.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets what is left of it, counting approved time off before its last day.
    /// </summary>
    public decimal Remaining { get; set; }

    /// <summary>
    /// Gets or sets the day it counts from.
    /// </summary>
    public DateOnly EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets its last day.
    /// </summary>
    public DateOnly ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets its note.
    /// </summary>
    public string Note { get; set; }
}
