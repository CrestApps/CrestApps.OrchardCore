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
    public decimal Available => Current + ProjectedAccrual - Upcoming - Pending;
}
