namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// How an open shift is given to the workers who claim it.
/// </summary>
public enum OpenShiftClaimPolicy
{
    /// <summary>
    /// A manager approves one of the claims.
    /// </summary>
    ManagerApproves,

    /// <summary>
    /// The first eligible worker to claim it gets it.
    /// </summary>
    FirstCome,

    /// <summary>
    /// At the claim deadline, the most senior eligible claimant gets it.
    /// </summary>
    BySeniority,
}
