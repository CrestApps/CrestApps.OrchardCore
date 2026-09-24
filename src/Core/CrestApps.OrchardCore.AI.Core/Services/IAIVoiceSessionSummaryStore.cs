using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Keeps the per-call <see cref="AIVoiceSessionSummary"/> records the AI usage report reads.
/// </summary>
/// <remarks>
/// A tenant without the usage analytics feature has a default that keeps nothing, so the voice loop can always
/// hand a summary over and the report can always ask for them.
/// </remarks>
public interface IAIVoiceSessionSummaryStore
{
    /// <summary>
    /// The summary already written for the call on this activity, or <see langword="null"/>.
    /// </summary>
    /// <param name="activityId">The activity the call belongs to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<AIVoiceSessionSummary> FindByActivityAsync(string activityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a summary.
    /// </summary>
    /// <param name="summary">The summary to save.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task SaveAsync(AIVoiceSessionSummary summary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a summary, so a better-informed one can take its place.
    /// </summary>
    /// <param name="summary">The summary to delete.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task DeleteAsync(AIVoiceSessionSummary summary, CancellationToken cancellationToken = default);

    /// <summary>
    /// The summaries written within the optional UTC date range, read from the index only.
    /// </summary>
    /// <param name="startDateUtc">The inclusive UTC start date.</param>
    /// <param name="endDateUtc">The inclusive UTC end date.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<AIVoiceSessionSummaryIndex>> GetAsync(DateTime? startDateUtc, DateTime? endDateUtc, CancellationToken cancellationToken = default);
}
