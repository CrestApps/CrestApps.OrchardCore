namespace CrestApps.OrchardCore.Staff.Models;

/// <summary>
/// How a staff member is employed.
/// </summary>
public enum EmploymentType
{
    /// <summary>
    /// A full-time employee.
    /// </summary>
    FullTime,

    /// <summary>
    /// A part-time employee.
    /// </summary>
    PartTime,

    /// <summary>
    /// A seasonal employee.
    /// </summary>
    Seasonal,

    /// <summary>
    /// A temporary employee.
    /// </summary>
    Temporary,

    /// <summary>
    /// An independent contractor.
    /// </summary>
    Contractor,

    /// <summary>
    /// A subcontractor's worker.
    /// </summary>
    Subcontractor,
}
