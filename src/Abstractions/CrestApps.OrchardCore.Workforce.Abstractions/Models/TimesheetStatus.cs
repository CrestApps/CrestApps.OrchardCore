namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Where a timesheet stands.
/// </summary>
public enum TimesheetStatus
{
    /// <summary>
    /// The period is being worked.
    /// </summary>
    Open,

    /// <summary>
    /// The worker attested to it and sent it for approval.
    /// </summary>
    Submitted,

    /// <summary>
    /// A manager approved it.
    /// </summary>
    Approved,

    /// <summary>
    /// A manager sent it back to be fixed.
    /// </summary>
    Rejected,

    /// <summary>
    /// The pay period is closed; it is ready for payroll and no longer changes.
    /// </summary>
    Locked,

    /// <summary>
    /// A manager opened it again after it was approved.
    /// </summary>
    Reopened,
}
