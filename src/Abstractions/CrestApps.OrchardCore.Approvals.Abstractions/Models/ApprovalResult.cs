namespace CrestApps.OrchardCore.Approvals.Models;

/// <summary>
/// A request, or why an action on it was refused.
/// </summary>
public sealed class ApprovalResult
{
    /// <summary>
    /// Gets or sets the request.
    /// </summary>
    public ApprovalRequest Request { get; set; }

    /// <summary>
    /// Gets or sets why it was refused.
    /// </summary>
    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether it succeeded.
    /// </summary>
    public bool Succeeded
        => Request is not null && Errors.Count == 0;
}

/// <summary>
/// Whether a person may decide a request, and why not.
/// </summary>
/// <param name="CanDecide">Whether the person may decide.</param>
/// <param name="Reason">Why not.</param>
public sealed record ApprovalEligibility(bool CanDecide, string Reason)
{
    /// <summary>
    /// Gets the answer that the person may decide.
    /// </summary>
    public static ApprovalEligibility Allowed { get; } = new(true, null);
}
