using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The worker editor.
/// </summary>
public class WorkerEditViewModel
{
    /// <summary>
    /// Gets or sets the worker id, or empty for a new worker.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the user the worker signs in as.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the phone number.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the employee number.
    /// </summary>
    public string EmployeeNumber { get; set; }

    /// <summary>
    /// Gets or sets the id in an HR or payroll system.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// Gets or sets the job title.
    /// </summary>
    public string JobTitle { get; set; }

    /// <summary>
    /// Gets or sets how the worker is employed.
    /// </summary>
    public string EmploymentType { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the hire date.
    /// </summary>
    public DateOnly? HireDate { get; set; }

    /// <summary>
    /// Gets or sets the seniority date.
    /// </summary>
    public DateOnly? SeniorityDate { get; set; }

    /// <summary>
    /// Gets or sets the last day worked.
    /// </summary>
    public DateOnly? TerminationDate { get; set; }

    /// <summary>
    /// Gets or sets the date of birth; only shown to users who may see sensitive details.
    /// </summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Gets or sets the time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the home base address.
    /// </summary>
    public string HomeBase { get; set; }

    /// <summary>
    /// Gets or sets the contracted hours a week.
    /// </summary>
    public decimal? ContractedHoursPerWeek { get; set; }

    /// <summary>
    /// Gets or sets the fewest hours a week.
    /// </summary>
    public decimal? MinHoursPerWeek { get; set; }

    /// <summary>
    /// Gets or sets the most hours a week.
    /// </summary>
    public decimal? MaxHoursPerWeek { get; set; }

    /// <summary>
    /// Gets or sets the most hours a day.
    /// </summary>
    public decimal? MaxHoursPerDay { get; set; }

    /// <summary>
    /// Gets or sets the weekly availability as text.
    /// </summary>
    public string Availability { get; set; }

    /// <summary>
    /// Gets or sets the team memberships, with a few empty rows to add new ones.
    /// </summary>
    public IList<TeamMembershipRowViewModel> Memberships { get; set; } = [];

    /// <summary>
    /// Gets or sets one proficiency row per enabled skill.
    /// </summary>
    public IList<WorkerSkillRowViewModel> Skills { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the editing user may see and change sensitive details.
    /// </summary>
    [BindNever]
    public bool CanSeeSensitive { get; set; }

    /// <summary>
    /// Gets or sets the worker's certificates, shown read-only.
    /// </summary>
    [BindNever]
    public IList<WorkerCertificateViewModel> Certificates { get; set; } = [];

    /// <summary>
    /// Gets or sets the teams to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the employment types to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> EmploymentTypeOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the statuses to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> StatusOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the time zones to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TimeZoneOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the certifications to add a certificate of.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> CertificationOptions { get; set; } = [];
}
