using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A person who works: an employee, a contractor or a crew member, with or without a user account.
/// </summary>
public sealed class WorkerProfile : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the name the worker is shown by.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the user the worker signs in as, or <see langword="null"/> for a worker with no account.
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
    /// Gets or sets the employee number, unique when set.
    /// </summary>
    public string EmployeeNumber { get; set; }

    /// <summary>
    /// Gets or sets the worker's id in an HR or payroll system.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// Gets or sets the job title.
    /// </summary>
    public string JobTitle { get; set; }

    /// <summary>
    /// Gets or sets how the worker is employed.
    /// </summary>
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;

    /// <summary>
    /// Gets or sets whether the worker can be scheduled.
    /// </summary>
    public WorkerStatus Status { get; set; } = WorkerStatus.Active;

    /// <summary>
    /// Gets or sets the day the worker started.
    /// </summary>
    public DateOnly? HireDate { get; set; }

    /// <summary>
    /// Gets or sets the day seniority counts from, when it differs from the hire date.
    /// </summary>
    public DateOnly? SeniorityDate { get; set; }

    /// <summary>
    /// Gets or sets the last day the worker worked.
    /// </summary>
    public DateOnly? TerminationDate { get; set; }

    /// <summary>
    /// Gets or sets the date of birth, used only for rules about young workers; sensitive.
    /// </summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Gets or sets the worker's line manager.
    /// </summary>
    public string ManagerWorkerId { get; set; }

    /// <summary>
    /// Gets or sets the worker's main team.
    /// </summary>
    public string PrimaryTeamId { get; set; }

    /// <summary>
    /// Gets or sets the teams the worker belongs to.
    /// </summary>
    public IList<TeamMembership> TeamMemberships { get; set; } = [];

    /// <summary>
    /// Gets or sets the worker's time zone; empty for their main team's.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets where the worker starts and ends their day, for field work.
    /// </summary>
    public WorkforceLocation HomeBase { get; set; }

    /// <summary>
    /// Gets or sets the hours a week the worker is contracted for.
    /// </summary>
    public decimal? ContractedHoursPerWeek { get; set; }

    /// <summary>
    /// Gets or sets the fewest hours a week the worker should be scheduled.
    /// </summary>
    public decimal? MinHoursPerWeek { get; set; }

    /// <summary>
    /// Gets or sets the most hours a week the worker may be scheduled.
    /// </summary>
    public decimal? MaxHoursPerWeek { get; set; }

    /// <summary>
    /// Gets or sets the most hours a day the worker may be scheduled.
    /// </summary>
    public decimal? MaxHoursPerDay { get; set; }

    /// <summary>
    /// Gets or sets the worker's weekly availability; empty when they can work any time.
    /// </summary>
    public IList<AvailabilityWindow> Availability { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC time the worker was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the worker was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets the teams the worker belongs to on a day.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns>The memberships that count that day.</returns>
    public IEnumerable<TeamMembership> MembershipsOn(DateOnly date)
    {
        return TeamMemberships.Where(membership => membership.IsEffectiveOn(date));
    }
}
