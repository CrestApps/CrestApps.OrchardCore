using CrestApps.OrchardCore.Approvals.Models;

namespace CrestApps.OrchardCore.Approvals.Services;

/// <summary>
/// Chooses the policy of a request: the one it names (by id or technical name), or the first enabled policy, by
/// <see cref="ApprovalPolicy.Order"/>, that serves its type and whose amount, currency and access scope conditions
/// match.
/// </summary>
public interface IApprovalPolicyResolver
{
    /// <summary>
    /// Returns the policy of a request.
    /// </summary>
    /// <param name="submission">What is asked.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The policy, or <see langword="null"/> when none is named or matches.</returns>
    Task<ApprovalPolicy> ResolveAsync(ApprovalSubmission submission, CancellationToken cancellationToken = default);
}
