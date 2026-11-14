using CrestApps.OrchardCore.Approvals.Models;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// Questions about a policy as a whole that the approver directory answers.
/// </summary>
public static class ApproverDirectoryExtensions
{
    /// <summary>
    /// Counts the people a policy's rules name today, or returns <see langword="null"/> when a rule names the subject's
    /// manager, which depends on each request.
    /// </summary>
    /// <param name="directory">The approver directory.</param>
    /// <param name="policy">The policy.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many people may decide, or <see langword="null"/>.</returns>
    public static async Task<int?> CountPeopleAsync(this IApproverDirectory directory, ApprovalPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(policy);

        if (policy.Approvers.Any(rule => rule.Kind == ApproverKind.Manager))
        {
            return null;
        }

        var people = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in policy.Approvers)
        {
            people.UnionWith(await directory.GetUserIdsAsync(rule, new ApprovalRequest(), cancellationToken));
        }

        return people.Count;
    }
}
