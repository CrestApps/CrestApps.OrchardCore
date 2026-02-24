namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Metadata stored on <see cref="AIProfile.Properties"/> to control
/// whether session metrics are captured for analytics purposes.
/// </summary>
public sealed class AIProfileAnalyticsMetadata
{
    /// <summary>
    /// Gets or sets whether session metrics (usage, latency, feedback)
    /// are captured for chat sessions using this profile.
    /// </summary>
    public bool EnableSessionMetrics { get; set; }
}
