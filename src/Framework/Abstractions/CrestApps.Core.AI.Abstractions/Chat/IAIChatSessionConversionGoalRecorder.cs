using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Chat;

/// <summary>
/// Persists host-specific conversion-goal evaluation results after the shared
/// post-close processor evaluates the configured goals.
/// </summary>
public interface IAIChatSessionConversionGoalRecorder
{
    /// <summary>
    /// Records evaluated conversion-goal results for the specified chat session.
    /// </summary>
    Task RecordConversionGoalsAsync(
        AIProfile profile,
        AIChatSession session,
        IReadOnlyList<ConversionGoalResult> goalResults,
        CancellationToken cancellationToken = default);
}
