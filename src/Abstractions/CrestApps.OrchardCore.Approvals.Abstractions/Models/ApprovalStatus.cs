using CrestApps.OrchardCore.StateMachines;

namespace CrestApps.OrchardCore.Approvals.Models;

/// <summary>
/// Where an approval request is.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// Waiting for decisions.
    /// </summary>
    Pending,

    /// <summary>
    /// Enough approvers approved.
    /// </summary>
    Approved,

    /// <summary>
    /// An approver rejected it.
    /// </summary>
    Rejected,

    /// <summary>
    /// Withdrawn before it was decided.
    /// </summary>
    Canceled,
}

/// <summary>
/// The moves an approval request may make.
/// </summary>
public static class ApprovalStatuses
{
    private static readonly TransitionTable<ApprovalStatus> _transitions = new(new Dictionary<ApprovalStatus, ApprovalStatus[]>
    {
        [ApprovalStatus.Pending] = [ApprovalStatus.Approved, ApprovalStatus.Rejected, ApprovalStatus.Canceled],
        [ApprovalStatus.Approved] = [],
        [ApprovalStatus.Rejected] = [],
        [ApprovalStatus.Canceled] = [],
    });

    /// <summary>
    /// Gets the allowed moves.
    /// </summary>
    public static TransitionTable<ApprovalStatus> Transitions
        => _transitions;
}
