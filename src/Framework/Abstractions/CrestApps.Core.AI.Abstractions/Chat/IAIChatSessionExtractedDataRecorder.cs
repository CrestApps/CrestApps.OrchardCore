using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Chat;

/// <summary>
/// Persists host-specific extracted-data snapshots so extracted chat-session values
/// can be queried later for reporting and analytics.
/// </summary>
public interface IAIChatSessionExtractedDataRecorder
{
    /// <summary>
    /// Records the current extracted-data snapshot for the specified chat session.
    /// Implementations should upsert an existing record when one already exists.
    /// </summary>
    Task RecordExtractedDataAsync(
        AIProfile profile,
        AIChatSession session,
        CancellationToken cancellationToken = default);
}
