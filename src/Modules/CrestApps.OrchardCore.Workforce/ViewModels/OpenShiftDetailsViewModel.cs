using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// An open shift with its claims and the workers who can take it.
/// </summary>
public class OpenShiftDetailsViewModel
{
    /// <summary>
    /// Gets or sets the open shift.
    /// </summary>
    public OpenShift Shift { get; set; }

    /// <summary>
    /// Gets or sets the name of its team.
    /// </summary>
    public string TeamName { get; set; }

    /// <summary>
    /// Gets or sets the name of its template.
    /// </summary>
    public string TemplateName { get; set; }

    /// <summary>
    /// Gets or sets the names of the skills it needs.
    /// </summary>
    public IList<string> SkillNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the name of the worker who got it.
    /// </summary>
    public string FilledWorkerName { get; set; }

    /// <summary>
    /// Gets or sets its claims, newest first.
    /// </summary>
    public IList<OpenShiftClaimRowViewModel> Claims { get; set; } = [];

    /// <summary>
    /// Gets or sets the workers who can take it.
    /// </summary>
    public IList<SelectListItem> EligibleOptions { get; set; } = [];
}

/// <summary>
/// One claim on an open shift.
/// </summary>
public class OpenShiftClaimRowViewModel
{
    /// <summary>
    /// Gets or sets the claim.
    /// </summary>
    public OpenShiftClaim Claim { get; set; }

    /// <summary>
    /// Gets or sets the claimant's name.
    /// </summary>
    public string WorkerName { get; set; }

    /// <summary>
    /// Gets or sets the claimant's seniority date.
    /// </summary>
    public DateOnly? SeniorityDate { get; set; }

    /// <summary>
    /// Gets or sets why the claimant can't take the shift now; empty when they can.
    /// </summary>
    public IList<string> Problems { get; set; } = [];
}
