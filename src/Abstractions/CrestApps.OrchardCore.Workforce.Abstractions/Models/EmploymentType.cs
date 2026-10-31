namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// How a worker is employed.
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
    /// Employed for a season.
    /// </summary>
    Seasonal,

    /// <summary>
    /// Employed for a fixed period.
    /// </summary>
    Temporary,

    /// <summary>
    /// An independent contractor.
    /// </summary>
    Contractor,

    /// <summary>
    /// Works for a subcontracting company.
    /// </summary>
    Subcontractor,
}
