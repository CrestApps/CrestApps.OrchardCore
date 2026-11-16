using CrestApps.OrchardCore.Approvals.Models;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// Questions about a policy's stages that the approver directory answers.
/// </summary>
public static class ApproverDirectoryExtensions
{
    /// <summary>
    /// Counts the people a stage's rules name today, or returns <see langword="null"/> when a rule depends on each
    /// request, such as the subject's manager.
    /// </summary>
    /// <param name="directory">The approver directory.</param>
    /// <param name="stage">The stage.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many people may decide the stage, or <see langword="null"/>.</returns>
    public static async Task<int?> CountPeopleAsync(this IApproverDirectory directory, ApprovalStage stage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(stage);

        if (stage.Approvers.Any(directory.DependsOnRequest))
        {
            return null;
        }

        var people = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in stage.Approvers)
        {
            people.UnionWith(await directory.GetUserIdsAsync(rule, new ApprovalRequest(), cancellationToken));
        }

        return people.Count;
    }
}
