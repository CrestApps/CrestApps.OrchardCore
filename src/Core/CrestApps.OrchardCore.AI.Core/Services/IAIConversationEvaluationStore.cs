using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Indexes;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Keeps conversation evaluations.
/// </summary>
public interface IAIConversationEvaluationStore
{
    /// <summary>
    /// Saves an evaluation.
    /// </summary>
    /// <param name="evaluation">The evaluation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task SaveAsync(AIConversationEvaluation evaluation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the newest evaluation of a session, or <see langword="null"/> when it was never evaluated.
    /// </summary>
    /// <param name="sessionId">The AI session.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<AIConversationEvaluation> FindLatestBySessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets every evaluation of a session.
    /// </summary>
    /// <param name="sessionId">The AI session.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<AIConversationEvaluation>> ListBySessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every evaluation of a session.
    /// </summary>
    /// <param name="sessionId">The AI session.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many were deleted.</returns>
    Task<int> DeleteBySessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the evaluation rows made in a window, optionally for one profile.
    /// </summary>
    /// <param name="profileId">The profile, or <see langword="null"/> for all of them.</param>
    /// <param name="fromUtc">The inclusive start of the window.</param>
    /// <param name="toUtc">The exclusive end of the window.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<AIConversationEvaluationIndex>> GetAsync(string profileId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
