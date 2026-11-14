using System.Security.Claims;
using CrestApps.OrchardCore.Approvals.Models;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// Answers the questions approver rules ask: does a person hold a permission or a role, who manages a staff member,
/// which user is a staff member, and who the people of a rule are, for notices.
/// </summary>
public interface IApproverDirectory
{
    /// <summary>
    /// Returns whether a person is named by a rule for a request.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="request">The request.</param>
    /// <param name="user">The person.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the rule names them.</returns>
    Task<bool> IsNamedAsync(ApproverRule rule, ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the user ids a rule names for a request.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The user ids.</returns>
    Task<IReadOnlyList<string>> GetUserIdsAsync(ApproverRule rule, ApprovalRequest request, CancellationToken cancellationToken = default);
}
