namespace CrestApps.OrchardCore.Staff.Models;

/// <summary>
/// Whether a staff member is working. The values keep the numbers Workforce stored for workers.
/// </summary>
public enum StaffStatus
{
    /// <summary>
    /// Working.
    /// </summary>
    Active,

    /// <summary>
    /// On leave; still employed.
    /// </summary>
    OnLeave,

    /// <summary>
    /// Suspended; not scheduled or assigned.
    /// </summary>
    Suspended,

    /// <summary>
    /// No longer works for the business.
    /// </summary>
    Terminated,
}
