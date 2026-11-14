namespace CrestApps.OrchardCore.Approvals.Models;

/// <summary>
/// What happened to an approval request.
/// </summary>
public enum ApprovalChangeKind
{
    /// <summary>
    /// Someone asked for approval.
    /// </summary>
    Requested,

    /// <summary>
    /// An approver approved, and more approvals are needed.
    /// </summary>
    PartlyApproved,

    /// <summary>
    /// Enough approvers approved.
    /// </summary>
    Approved,

    /// <summary>
    /// An approver rejected it.
    /// </summary>
    Rejected,

    /// <summary>
    /// It was withdrawn.
    /// </summary>
    Canceled,
}

/// <summary>
/// A change of an approval request, told to the observers.
/// </summary>
public sealed class ApprovalChange
{
    /// <summary>
    /// Gets or sets the request after the change.
    /// </summary>
    public ApprovalRequest Request { get; set; }

    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public ApprovalChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the decision that made the change, when there is one.
    /// </summary>
    public ApprovalDecision Decision { get; set; }

    /// <summary>
    /// Gets or sets who made the change.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets the user id of who made the change.
    /// </summary>
    public string ActorId { get; set; }
}
