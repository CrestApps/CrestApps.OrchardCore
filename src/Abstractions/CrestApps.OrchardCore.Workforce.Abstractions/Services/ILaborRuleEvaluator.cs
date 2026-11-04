using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Checks shifts against the labor rule set that applies to a worker.
/// </summary>
public interface ILaborRuleEvaluator
{
    /// <summary>
    /// Returns the enabled rule set that applies to a worker on a team: the one assigned to the worker alone, or
    /// else the nearest team's up the tree, for the worker's employment type.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="teamId">The team.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The rule set, or <see langword="null"/>.</returns>
    Task<LaborRuleSet> ResolveRuleSetAsync(WorkerProfile worker, string teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the shifts, with the worker's published shifts and approved time off around them.
    /// </summary>
    /// <param name="request">The worker and the shifts.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was found.</returns>
    Task<LaborRuleResult> EvaluateAsync(LaborRuleRequest request, CancellationToken cancellationToken = default);
}
