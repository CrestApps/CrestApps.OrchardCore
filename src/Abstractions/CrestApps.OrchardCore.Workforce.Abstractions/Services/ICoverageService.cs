using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Keeps coverage requirements and works out, per interval, how many people are needed and how many are scheduled.
/// </summary>
public interface ICoverageService
{
    /// <summary>
    /// Lists a team's coverage requirements.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The requirements.</returns>
    Task<IReadOnlyList<CoverageRequirement>> ListRequirementsAsync(string teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a coverage requirement.
    /// </summary>
    /// <param name="id">The requirement id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The requirement, or <see langword="null"/>.</returns>
    Task<CoverageRequirement> FindRequirementAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a new or changed coverage requirement.
    /// </summary>
    /// <param name="requirement">The requirement.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether it was saved, or why not.</returns>
    Task<PlanningResult<CoverageRequirement>> SaveRequirementAsync(CoverageRequirement requirement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a coverage requirement.
    /// </summary>
    /// <param name="id">The requirement id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when it existed.</returns>
    Task<bool> DeleteRequirementAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Works out the coverage of a team, interval by interval, against a draft scenario or the published shifts.
    /// </summary>
    /// <param name="query">Which coverage.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The intervals, in order.</returns>
    Task<IReadOnlyList<CoverageInterval>> ComputeAsync(CoverageQuery query, CancellationToken cancellationToken = default);
}
