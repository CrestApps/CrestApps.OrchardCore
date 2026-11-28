using CrestApps.Core.Models;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Assigns a time-off policy to one worker, or takes the worker's own assignments of it away, as the worker's time-off
/// page does. A policy assigned to the worker alone takes precedence over their teams' policies of its type.
/// </summary>
internal static class TimeOffPolicyAssignments
{
    /// <summary>
    /// Adds an assignment of the policy to the worker from and to the given days, and saves the policy when it is valid.
    /// </summary>
    /// <param name="policyManager">The policy manager.</param>
    /// <param name="policy">The policy.</param>
    /// <param name="workerId">The worker.</param>
    /// <param name="effectiveFrom">The first day, or <see langword="null"/> for no start.</param>
    /// <param name="effectiveTo">The last day, or <see langword="null"/> for no end.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The validation of the changed policy; the policy is saved only when it succeeded.</returns>
    public static async Task<ValidationResultDetails> AssignAsync(ITimeOffPolicyManager policyManager, TimeOffPolicy policy, string workerId, DateOnly? effectiveFrom, DateOnly? effectiveTo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policyManager);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrEmpty(workerId);

        policy.Assignments.Add(new TimeOffPolicyAssignment { WorkerId = workerId, EffectiveFrom = effectiveFrom, EffectiveTo = effectiveTo });

        return await SaveAsync(policyManager, policy, cancellationToken);
    }

    /// <summary>
    /// Removes every assignment of the policy to the worker alone (not the worker's teams' assignments), and saves the
    /// policy when it is valid.
    /// </summary>
    /// <param name="policyManager">The policy manager.</param>
    /// <param name="policy">The policy.</param>
    /// <param name="workerId">The worker.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The number of assignments removed, and the validation of the changed policy.</returns>
    public static async Task<(int Removed, ValidationResultDetails Validation)> UnassignAsync(ITimeOffPolicyManager policyManager, TimeOffPolicy policy, string workerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policyManager);
        ArgumentNullException.ThrowIfNull(policy);

        var removed = 0;

        foreach (var assignment in policy.Assignments.Where(assignment => assignment.WorkerId == workerId).ToArray())
        {
            policy.Assignments.Remove(assignment);
            removed++;
        }

        return (removed, await SaveAsync(policyManager, policy, cancellationToken));
    }

    private static async Task<ValidationResultDetails> SaveAsync(ITimeOffPolicyManager policyManager, TimeOffPolicy policy, CancellationToken cancellationToken)
    {
        var validation = await policyManager.ValidateAsync(policy, cancellationToken);

        if (validation.Succeeded)
        {
            await policyManager.UpdateAsync(policy, cancellationToken: cancellationToken);
        }

        return validation;
    }
}
