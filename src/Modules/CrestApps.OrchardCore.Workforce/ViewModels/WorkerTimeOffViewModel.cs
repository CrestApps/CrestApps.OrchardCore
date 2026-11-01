using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// A worker's time off: balances, the ledger, the policies assigned to them alone, and the forms that change them.
/// </summary>
public class WorkerTimeOffViewModel
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    [BindNever]
    public WorkerProfile Worker { get; set; }

    /// <summary>
    /// Gets or sets the day the balances are for.
    /// </summary>
    [BindNever]
    public DateOnly Today { get; set; }

    /// <summary>
    /// Gets or sets the balances.
    /// </summary>
    [BindNever]
    public IList<TimeOffBalance> Balances { get; set; } = [];

    /// <summary>
    /// Gets or sets the ledger list.
    /// </summary>
    [BindNever]
    public AdminListContext Ledger { get; set; }

    /// <summary>
    /// Gets or sets the policies assigned to the worker alone, with the dates.
    /// </summary>
    [BindNever]
    public IList<(TimeOffPolicy Policy, TimeOffPolicyAssignment Assignment)> Assignments { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the user can change the balances.
    /// </summary>
    [BindNever]
    public bool CanAdjust { get; set; }

    /// <summary>
    /// Gets or sets whether the user can assign policies.
    /// </summary>
    [BindNever]
    public bool CanAssign { get; set; }

    /// <summary>
    /// Gets or sets the policies the worker has, for the adjustment form.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> PolicyOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets every enabled policy, for the assignment form.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AllPolicyOptions { get; set; } = [];
}
