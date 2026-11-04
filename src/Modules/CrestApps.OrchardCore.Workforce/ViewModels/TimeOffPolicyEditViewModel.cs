using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The fields of a time-off policy.
/// </summary>
public class TimeOffPolicyEditViewModel
{
    /// <summary>
    /// Gets or sets the policy id, or empty for a new policy.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the time-off type.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets how it accrues.
    /// </summary>
    public string AccrualMethod { get; set; }

    /// <summary>
    /// Gets or sets the accrual period.
    /// </summary>
    public string Frequency { get; set; }

    /// <summary>
    /// Gets or sets the first day of a weekly or biweekly period.
    /// </summary>
    public DateOnly? AnchorDate { get; set; }

    /// <summary>
    /// Gets or sets the rate.
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// Gets or sets the rates after months of service, one per line.
    /// </summary>
    public string TenureTiers { get; set; }

    /// <summary>
    /// Gets or sets the waiting period.
    /// </summary>
    public int WaitingPeriodDays { get; set; }

    /// <summary>
    /// Gets or sets the balance cap.
    /// </summary>
    public decimal? MaxBalance { get; set; }

    /// <summary>
    /// Gets or sets the most that carries over.
    /// </summary>
    public decimal? CarryoverMax { get; set; }

    /// <summary>
    /// Gets or sets the month of the reset date.
    /// </summary>
    public int ResetMonth { get; set; }

    /// <summary>
    /// Gets or sets the day of the reset date.
    /// </summary>
    public int ResetDay { get; set; }

    /// <summary>
    /// Gets or sets how far below zero the balance may go.
    /// </summary>
    public decimal NegativeBalanceLimit { get; set; }

    /// <summary>
    /// Gets or sets the teams the policy applies to.
    /// </summary>
    public string[] TeamIds { get; set; }

    /// <summary>
    /// Gets or sets the first day the team assignments count.
    /// </summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day the team assignments count.
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>
    /// Gets or sets whether it is in use.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets how many workers are assigned one by one.
    /// </summary>
    [BindNever]
    public int WorkerAssignments { get; set; }

    /// <summary>
    /// Gets or sets the time-off types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the teams.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];
}
