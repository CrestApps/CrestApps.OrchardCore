namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Where a claim on an open shift stands.
/// </summary>
public enum OpenShiftClaimStatus
{
    /// <summary>
    /// Waiting for a decision.
    /// </summary>
    Pending,

    /// <summary>
    /// The claimant got the shift.
    /// </summary>
    Approved,

    /// <summary>
    /// Turned down, or the shift went to someone else.
    /// </summary>
    Declined,

    /// <summary>
    /// The claimant took it back.
    /// </summary>
    Withdrawn,
}
