using System.Security.Claims;
using CrestApps.OrchardCore.Approvals.Models;

namespace CrestApps.OrchardCore.Approvals.Services;

/// <summary>
/// The rules of approvals: a request follows its policy, the two built-in rules apply unless the policy turns them
/// off, one rejection rejects, enough approvals approve, and the subject's handler is told inside the same unit of
/// work. It checks no admin permission; the caller does.
/// </summary>
public interface IApprovalService
{
    /// <summary>
    /// Asks for approval. A subject with a pending request under the same policy gets that request back.
    /// </summary>
    /// <param name="submission">What is asked.</param>
    /// <param name="requester">Who asks, or <see langword="null"/> for a workflow.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The request, or why it was refused.</returns>
    Task<ApprovalResult> RequestAsync(ApprovalSubmission submission, ClaimsPrincipal requester, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether a person may decide a request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="user">The person.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether they may, and why not.</returns>
    Task<ApprovalEligibility> GetEligibilityAsync(ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a person's decision.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="user">The person.</param>
    /// <param name="kind">Approve or reject.</param>
    /// <param name="note">A note.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The request, or why the decision was refused.</returns>
    Task<ApprovalResult> DecideAsync(ApprovalRequest request, ClaimsPrincipal user, ApprovalDecisionKind kind, string note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws a pending request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when it was already decided.</returns>
    Task<bool> CancelAsync(ApprovalRequest request, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the user ids of the people who may decide a request, for notices: the approvers the policy names, less
    /// the requester and the people the subject is about unless the policy allows them, less those who already decided.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The user ids.</returns>
    Task<IReadOnlyList<string>> GetApproverUserIdsAsync(ApprovalRequest request, CancellationToken cancellationToken = default);
}
