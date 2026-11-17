namespace CrestApps.OrchardCore.Approvals.Models;

/// <summary>
/// What an approver decided.
/// </summary>
public enum ApprovalDecisionKind
{
    /// <summary>
    /// Approved.
    /// </summary>
    Approve,

    /// <summary>
    /// Rejected.
    /// </summary>
    Reject,
}

/// <summary>
/// One approver's decision on a request.
/// </summary>
public sealed class ApprovalDecision
{
    /// <summary>
    /// Gets or sets the approver's user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the approver's user name.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the decision.
    /// </summary>
    public ApprovalDecisionKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the approver's note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets the index of the stage the decision was made in, from 0.
    /// </summary>
    public int Stage { get; set; }

    /// <summary>
    /// Gets or sets when the decision was made.
    /// </summary>
    public DateTime DecidedUtc { get; set; }
}
