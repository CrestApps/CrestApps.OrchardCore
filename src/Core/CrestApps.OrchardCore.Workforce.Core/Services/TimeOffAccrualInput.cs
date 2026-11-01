using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// What the accrual engine needs to work out what a policy owes a worker.
/// </summary>
internal sealed class TimeOffAccrualInput
{
    /// <summary>
    /// Gets or sets the policy.
    /// </summary>
    public TimeOffPolicy Policy { get; set; }

    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public WorkerProfile Worker { get; set; }

    /// <summary>
    /// Gets or sets the first day the policy can apply to the worker, before the waiting period.
    /// </summary>
    public DateOnly EligibleFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day the policy can apply to the worker; empty for no end.
    /// </summary>
    public DateOnly? EligibleTo { get; set; }

    /// <summary>
    /// Gets or sets the day to accrue up to: periods that end on or before it.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the worker's ledger under the policy so far.
    /// </summary>
    public IReadOnlyList<TimeOffLedgerEntry> Existing { get; set; } = [];

    /// <summary>
    /// Gets or sets the minutes worked in each period, by the period's last day.
    /// </summary>
    public IReadOnlyDictionary<DateOnly, int> WorkedMinutes { get; set; } = new Dictionary<DateOnly, int>();

    /// <summary>
    /// Gets or sets whether the policy is the one that applies to the worker on a day; empty when it always is.
    /// </summary>
    public Func<DateOnly, bool> AppliesOn { get; set; }
}
