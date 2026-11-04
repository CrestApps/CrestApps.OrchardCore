using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The Workforce section of the user editor.
/// </summary>
public class UserWorkforceViewModel
{
    /// <summary>
    /// Gets or sets the user's worker, if they are one.
    /// </summary>
    [BindNever]
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets whether to make the user a worker.
    /// </summary>
    public bool MakeWorker { get; set; }

    /// <summary>
    /// Gets or sets the employee number.
    /// </summary>
    public string EmployeeNumber { get; set; }

    /// <summary>
    /// Gets or sets the job title.
    /// </summary>
    public string JobTitle { get; set; }

    /// <summary>
    /// Gets or sets how they are employed.
    /// </summary>
    public string EmploymentType { get; set; }

    /// <summary>
    /// Gets or sets their status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets when they were hired.
    /// </summary>
    public DateOnly? HireDate { get; set; }

    /// <summary>
    /// Gets or sets their main team.
    /// </summary>
    public string PrimaryTeamId { get; set; }

    /// <summary>
    /// Gets or sets their contracted hours a week.
    /// </summary>
    public decimal? ContractedHoursPerWeek { get; set; }

    /// <summary>
    /// Gets or sets the teams to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the employment types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> EmploymentTypeOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the statuses.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> StatusOptions { get; set; } = [];
}
