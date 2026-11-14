using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// Stores approval requests.
/// </summary>
public interface IApprovalRequestStore : ICatalog<ApprovalRequest>
{
    /// <summary>
    /// Finds the pending request about a subject under a policy.
    /// </summary>
    /// <param name="subjectKind">The subject's kind.</param>
    /// <param name="subjectId">The subject's id.</param>
    /// <param name="policyId">The policy, or <see langword="null"/> for any.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The request, or <see langword="null"/>.</returns>
    Task<ApprovalRequest> FindPendingAsync(string subjectKind, string subjectId, string policyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the requests about a subject, newest first.
    /// </summary>
    /// <param name="subjectKind">The subject's kind.</param>
    /// <param name="subjectId">The subject's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The requests.</returns>
    Task<IReadOnlyList<ApprovalRequest>> ListBySubjectAsync(string subjectKind, string subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a page of requests: only those the query's access filter allows.
    /// </summary>
    /// <param name="query">What the page shows.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count and the page.</returns>
    Task<(int Count, IReadOnlyList<ApprovalRequest> Requests)> ListPageAsync(ApprovalRequestQuery query, int skip, int take, CancellationToken cancellationToken = default);
}
