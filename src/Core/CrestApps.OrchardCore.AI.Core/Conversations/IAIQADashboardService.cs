namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Reads the figures of the QA dashboard from the conversation evaluations of a period.
/// </summary>
public interface IAIQADashboardService
{
    /// <summary>
    /// Builds the dashboard of a period.
    /// </summary>
    /// <param name="label">The evaluations' label, such as a QA cohort's, or <see langword="null"/> for every evaluation.</param>
    /// <param name="fromUtc">The start of the period.</param>
    /// <param name="toUtc">The end of the period.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The figures.</returns>
    Task<AIQADashboard> BuildAsync(string label, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
