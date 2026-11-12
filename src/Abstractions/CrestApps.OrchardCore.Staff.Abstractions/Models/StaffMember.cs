using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Staff.Models;

/// <summary>
/// A person who works for the business: an employee, a contractor or a crew member, with or without a user account.
/// Other modules keep their own data for the person in <see cref="ExtensibleEntity.Properties"/> and add their own
/// cards to the staff editor with display drivers.
/// </summary>
public sealed class StaffMember : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the name shown for the person. For a person with a user account, Staff keeps it in step with the
    /// user's display name so lists can be searched and sorted; other modules read it here and never copy it.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the linked Orchard user, or <see langword="null"/> for a person without an account.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the work e-mail. For a person with a user account, Staff keeps it in step with the user's e-mail.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the work phone, normalized to E.164 when it can be read.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the employee number. It is unique when set, and doubles as the badge number.
    /// </summary>
    public string EmployeeNumber { get; set; }

    /// <summary>
    /// Gets or sets the person's id in an outside HR or payroll system.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// Gets or sets the job title.
    /// </summary>
    public string JobTitle { get; set; }

    /// <summary>
    /// Gets or sets how the person is employed.
    /// </summary>
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;

    /// <summary>
    /// Gets or sets whether the person is working.
    /// </summary>
    public StaffStatus Status { get; set; } = StaffStatus.Active;

    /// <summary>
    /// Gets or sets the day the person was hired.
    /// </summary>
    public DateOnly? HireDate { get; set; }

    /// <summary>
    /// Gets or sets the day seniority counts from, when it is not the hire date.
    /// </summary>
    public DateOnly? SeniorityDate { get; set; }

    /// <summary>
    /// Gets or sets the person's last working day.
    /// </summary>
    public DateOnly? TerminationDate { get; set; }

    /// <summary>
    /// Gets or sets the date of birth. Sensitive: only people with <see cref="StaffPermissions.ViewSensitiveStaffData"/> see it.
    /// </summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Gets or sets the staff member the person reports to.
    /// </summary>
    public string ManagerId { get; set; }

    /// <summary>
    /// Gets or sets the main team; it defaults to the first membership.
    /// </summary>
    public string PrimaryTeamId { get; set; }

    /// <summary>
    /// Gets or sets the teams the person belongs to, each with its dates and role.
    /// </summary>
    public IList<TeamMembership> TeamMemberships { get; set; } = [];

    /// <summary>
    /// Gets or sets the person's IANA time zone; empty means the main team's, then the site's.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets where the person starts their day, such as for field work.
    /// </summary>
    public StaffLocation HomeBase { get; set; }

    /// <summary>
    /// Gets or sets the name of the place the person works at, such as an office, store or depot.
    /// </summary>
    public string WorkLocation { get; set; }

    /// <summary>
    /// Gets or sets the hours a week of the person's contract.
    /// </summary>
    public decimal? ContractedHoursPerWeek { get; set; }

    /// <summary>
    /// Gets or sets the kinds of work the person does, as registered by other modules (such as an agent, a technician or a cashier).
    /// </summary>
    public IList<string> Capabilities { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC time the record was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the record was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets the team memberships in effect on a day.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns>The memberships.</returns>
    public IEnumerable<TeamMembership> MembershipsOn(DateOnly date)
    {
        return TeamMemberships.Where(membership => membership.IsEffectiveOn(date));
    }

    /// <summary>
    /// Gets whether the person has a capability.
    /// </summary>
    /// <param name="capability">The capability name.</param>
    /// <returns><see langword="true"/> when the person has it.</returns>
    public bool HasCapability(string capability)
    {
        return !string.IsNullOrEmpty(capability) && Capabilities.Any(item => string.Equals(item, capability, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets whether the person is working on a day: active or on leave, and hired and not yet gone.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns><see langword="true"/> when the person is employed on that day.</returns>
    public bool IsEmployedOn(DateOnly date)
    {
        return Status is StaffStatus.Active or StaffStatus.OnLeave
            && (HireDate is null || HireDate <= date)
            && (TerminationDate is null || TerminationDate >= date);
    }
}
