namespace CrestApps.Core.AI.Models;

/// <summary>
/// Metadata stored on <see cref="AIProfile.Properties"/> to control
/// whether session metrics are captured for analytics purposes.
/// </summary>
public sealed class AnalyticsMetadata
{
    /// <summary>
    /// Gets or sets whether session metrics (usage, latency, feedback)
    /// are captured for chat sessions using this profile.
    /// </summary>
    public bool EnableSessionMetrics { get; set; }

    /// <summary>
    /// Gets or sets whether AI-based resolution detection is enabled.
    /// When enabled, the system uses AI to semantically determine whether
    /// a conversation was resolved, instead of relying solely on timeout-based abandonment.
    /// This setting operates independently of <see cref="EnableSessionMetrics"/>.
    /// </summary>
    public bool EnableAIResolutionDetection { get; set; } = true;

    /// <summary>
    /// Gets or sets whether conversion metrics are enabled for this profile.
    /// When enabled, the system uses AI to evaluate each session against configured goals
    /// and assigns scores to measure session success.
    /// This setting operates independently of <see cref="EnableSessionMetrics"/>.
    /// </summary>
    public bool EnableConversionMetrics { get; set; }

    /// <summary>
    /// Gets or sets the list of conversion goals to evaluate for each session.
    /// Each goal is scored by AI after the session closes.
    /// </summary>
    public List<ConversionGoal> ConversionGoals { get; set; } = [];
}
