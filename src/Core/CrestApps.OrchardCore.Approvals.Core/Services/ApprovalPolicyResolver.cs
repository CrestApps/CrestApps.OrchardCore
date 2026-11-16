using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// The default <see cref="IApprovalPolicyResolver"/>.
/// </summary>
public sealed class ApprovalPolicyResolver : IApprovalPolicyResolver
{
    private readonly INamedCatalogManager<ApprovalPolicy> _policies;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalPolicyResolver"/> class.
    /// </summary>
    /// <param name="policies">The policies.</param>
    public ApprovalPolicyResolver(INamedCatalogManager<ApprovalPolicy> policies)
    {
        _policies = policies;
    }

    /// <inheritdoc />
    public async Task<ApprovalPolicy> ResolveAsync(ApprovalSubmission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        // A policy the caller names wins, enabled or not: a module's own policy is chosen on purpose.
        if (!string.IsNullOrWhiteSpace(submission.Policy))
        {
            var named = submission.Policy.Trim();

            return await _policies.FindByIdAsync(named, cancellationToken) ?? await _policies.FindByNameAsync(named, cancellationToken);
        }

        return (await _policies.GetAllAsync(cancellationToken))
            .Where(policy => policy.Enabled && Matches(policy, submission))
            .OrderBy(policy => policy.Order)
            .ThenBy(policy => policy.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>
    /// Returns whether a policy's type and conditions match what is asked.
    /// </summary>
    /// <param name="policy">The policy.</param>
    /// <param name="submission">What is asked.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    public static bool Matches(ApprovalPolicy policy, ApprovalSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(submission);

        if (!policy.Serves(submission.SubjectKind?.Trim()))
        {
            return false;
        }

        if ((policy.MinimumAmount is not null || policy.MaximumAmount is not null) && submission.Amount is not { } amount)
        {
            return false;
        }

        if (policy.MinimumAmount is { } minimum && submission.Amount < minimum)
        {
            return false;
        }

        if (policy.MaximumAmount is { } maximum && submission.Amount > maximum)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(policy.Currency) && !string.Equals(policy.Currency.Trim(), submission.Currency?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(policy.AccessScope) || string.Equals(policy.AccessScope.Trim(), submission.AccessScope?.Trim(), StringComparison.Ordinal);
    }
}
