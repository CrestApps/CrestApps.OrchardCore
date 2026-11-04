namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Where a predictability pay entry stands.
/// </summary>
public enum PredictabilityPayStatus
{
    /// <summary>
    /// Waiting for payroll to approve it.
    /// </summary>
    Pending,

    /// <summary>
    /// Approved to be paid.
    /// </summary>
    Approved,

    /// <summary>
    /// Not owed, with a reason.
    /// </summary>
    Waived,

    /// <summary>
    /// Sent to payroll.
    /// </summary>
    Exported,
}
