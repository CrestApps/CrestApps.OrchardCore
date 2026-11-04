using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// How workers earn and keep time off of one type: the accrual, caps, carryover and who it applies to.
/// </summary>
public sealed class TimeOffPolicy : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the policy name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the time-off type the policy is for.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets how the policy adds to the balance.
    /// </summary>
    public TimeOffAccrualMethod AccrualMethod { get; set; } = TimeOffAccrualMethod.PerPeriod;

    /// <summary>
    /// Gets or sets how long an accrual period is, for per-period and per-hour accrual.
    /// </summary>
    public TimeOffAccrualFrequency Frequency { get; set; } = TimeOffAccrualFrequency.Monthly;

    /// <summary>
    /// Gets or sets the first day of a weekly or biweekly period; empty for the Monday on or before the first day
    /// the worker is eligible.
    /// </summary>
    public DateOnly? AnchorDate { get; set; }

    /// <summary>
    /// Gets or sets the amount: per period, per hour worked or per year, in the type's unit.
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// Gets or sets the rates that apply after months of service, which replace <see cref="Rate"/>.
    /// </summary>
    public IList<TimeOffTenureTier> TenureTiers { get; set; } = [];

    /// <summary>
    /// Gets or sets how many days after the worker starts accrual begins.
    /// </summary>
    public int WaitingPeriodDays { get; set; }

    /// <summary>
    /// Gets or sets the most the balance can reach through accrual; empty for no cap.
    /// </summary>
    public decimal? MaxBalance { get; set; }

    /// <summary>
    /// Gets or sets the most that carries over at the reset date; empty to carry over everything.
    /// </summary>
    public decimal? CarryoverMax { get; set; }

    /// <summary>
    /// Gets or sets the month of the reset date.
    /// </summary>
    public int ResetMonth { get; set; } = 1;

    /// <summary>
    /// Gets or sets the day of the reset date.
    /// </summary>
    public int ResetDay { get; set; } = 1;

    /// <summary>
    /// Gets or sets how far below zero the balance may go.
    /// </summary>
    public decimal NegativeBalanceLimit { get; set; }

    /// <summary>
    /// Gets or sets the teams and workers the policy applies to.
    /// </summary>
    public IList<TimeOffPolicyAssignment> Assignments { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the policy is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the policy was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the policy was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Returns the rate for a worker with a number of months of service.
    /// </summary>
    /// <param name="monthsOfService">The months of service.</param>
    /// <returns>The rate.</returns>
    public decimal RateFor(int monthsOfService)
    {
        var tier = TenureTiers
            .Where(item => item.FromMonths <= monthsOfService)
            .OrderByDescending(item => item.FromMonths)
            .FirstOrDefault();

        return tier?.Rate ?? Rate;
    }
}
