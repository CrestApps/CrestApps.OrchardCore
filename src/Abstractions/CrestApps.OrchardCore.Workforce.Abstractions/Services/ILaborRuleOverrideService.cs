using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Records and lists the broken labor rules someone went ahead with.
/// </summary>
public interface ILaborRuleOverrideService
{
    /// <summary>
    /// Records overrides of violations with a reason.
    /// </summary>
    /// <param name="violations">The violations gone ahead with.</param>
    /// <param name="reason">Why.</param>
    /// <param name="source">What the change was, such as Publish.</param>
    /// <param name="periodId">The schedule, for a publish.</param>
    /// <param name="teamId">The team.</param>
    /// <param name="actor">Who overrode them.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The records.</returns>
    Task<IReadOnlyList<LaborRuleOverride>> RecordAsync(IEnumerable<LaborRuleViolation> violations, string reason, string source, string periodId, string teamId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists overrides, newest first.
    /// </summary>
    /// <param name="teamIds">The teams; <see langword="null"/> for every team.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The number of overrides and one page of them.</returns>
    Task<(int Count, IReadOnlyList<LaborRuleOverride> Items)> PageAsync(IReadOnlyCollection<string> teamIds, int skip, int take, CancellationToken cancellationToken = default);
}
