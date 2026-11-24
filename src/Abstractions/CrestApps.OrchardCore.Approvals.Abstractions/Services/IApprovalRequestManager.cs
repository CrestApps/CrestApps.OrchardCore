using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;

namespace CrestApps.OrchardCore.Approvals.Services;

/// <summary>
/// Manages approval requests, so their handlers run.
/// </summary>
public interface IApprovalRequestManager : ICatalogManager<ApprovalRequest>
{
    /// <summary>
    /// Finds the pending request about a subject under a policy.
    /// </summary>
    /// <param name="subjectKind">The subject's kind.</param>
    /// <param name="subjectId">The subject's id.</param>
    /// <param name="policyId">The policy, or <see langword="null"/> for any.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The request, or <see langword="null"/>.</returns>
    Task<ApprovalRequest> FindPendingAsync(string subjectKind, string subjectId, string policyId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the requests about a subject, newest first.
    /// </summary>
    /// <param name="subjectKind">The subject's kind.</param>
    /// <param name="subjectId">The subject's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The requests.</returns>
    Task<IReadOnlyList<ApprovalRequest>> ListBySubjectAsync(string subjectKind, string subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a request as it is stored now, not the copy loaded earlier in this unit of work, so a decision made under
    /// a lock sees what another approver committed meanwhile.
    /// </summary>
    /// <param name="loaded">The request loaded earlier, which stops being tracked.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The stored request, or <see langword="null"/> when it was deleted.</returns>
    Task<ApprovalRequest> FindCommittedAsync(ApprovalRequest loaded, CancellationToken cancellationToken = default);
}
