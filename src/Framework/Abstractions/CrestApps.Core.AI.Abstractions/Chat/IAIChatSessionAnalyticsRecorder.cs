using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Chat;

/// <summary>
/// Persists host-specific chat session analytics after shared post-close analysis
/// has determined the final session metrics and resolution status.
/// </summary>
public interface IAIChatSessionAnalyticsRecorder
{
    /// <summary>
    /// Records end-of-session analytics for the specified chat session.
    /// </summary>
    Task RecordSessionEndedAsync(
        AIProfile profile,
        AIChatSession session,
        IReadOnlyList<AIChatSessionPrompt> prompts,
        bool isResolved,
        CancellationToken cancellationToken = default);
}
