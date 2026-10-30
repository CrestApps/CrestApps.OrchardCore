using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Remembers which AI conversation metric alerts are open, so a check that runs every few minutes says when an
/// alert starts and when it resolves instead of repeating itself on every run.
/// </summary>
public sealed class AIConversationAlertState : Document
{
    /// <summary>
    /// Gets the open alerts, keyed by the workflow type and the activity that checks them.
    /// </summary>
    public Dictionary<string, AIConversationOpenAlert> Open { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>
/// An alert that is open.
/// </summary>
public sealed class AIConversationOpenAlert
{
    /// <summary>
    /// Gets or sets when the alert started.
    /// </summary>
    public DateTime SinceUtc { get; set; }

    /// <summary>
    /// Gets or sets the value that started it.
    /// </summary>
    public double Value { get; set; }
}
