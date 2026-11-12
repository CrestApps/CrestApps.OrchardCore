using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Staff.Models;

/// <summary>
/// One change to a staff member's employment, kept so payroll and compliance can tell who held which job, status,
/// manager or team on a day. Entries are only ever added.
/// </summary>
public sealed class StaffHistoryEntry : CatalogItem
{
    /// <summary>
    /// Gets or sets the staff member.
    /// </summary>
    public string StaffMemberId { get; set; }

    /// <summary>
    /// Gets or sets what changed, one of <see cref="StaffHistoryFields"/>.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Gets or sets the value before the change, as text.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the value after the change, as text.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the user who made the change, when known.
    /// </summary>
    public string ChangedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the change.
    /// </summary>
    public DateTime ChangedUtc { get; set; }
}

/// <summary>
/// The fields <see cref="StaffHistoryEntry"/> records.
/// </summary>
public static class StaffHistoryFields
{
    /// <summary>
    /// The record was created.
    /// </summary>
    public const string Created = "Created";

    /// <summary>
    /// The job title.
    /// </summary>
    public const string JobTitle = "JobTitle";

    /// <summary>
    /// The status.
    /// </summary>
    public const string Status = "Status";

    /// <summary>
    /// The employment type.
    /// </summary>
    public const string EmploymentType = "EmploymentType";

    /// <summary>
    /// The manager (a staff member id).
    /// </summary>
    public const string Manager = "Manager";

    /// <summary>
    /// The team memberships.
    /// </summary>
    public const string Teams = "Teams";

    /// <summary>
    /// The work location.
    /// </summary>
    public const string WorkLocation = "WorkLocation";

    /// <summary>
    /// The termination date.
    /// </summary>
    public const string TerminationDate = "TerminationDate";
}
