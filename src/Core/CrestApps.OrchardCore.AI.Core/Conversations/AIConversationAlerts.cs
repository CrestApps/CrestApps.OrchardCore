namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// How a metric is compared with the threshold.
/// </summary>
public enum AIConversationAlertCondition
{
    /// <summary>
    /// The value is above the threshold.
    /// </summary>
    Above,

    /// <summary>
    /// The value is below the threshold.
    /// </summary>
    Below,

    /// <summary>
    /// The value rose by more than the threshold, in percent, from the window before.
    /// </summary>
    RisesByPercent,

    /// <summary>
    /// The value fell by more than the threshold, in percent, from the window before.
    /// </summary>
    FallsByPercent,
}

/// <summary>
/// Decides whether a measured metric crosses an alert threshold, for the alert rules and the
/// <c>Check AI Conversation Metric</c> task alike.
/// </summary>
public static class AIConversationAlerts
{
    /// <summary>
    /// Whether the current value crosses the threshold. A window with fewer samples than the minimum never does,
    /// and a change in percent needs a previous value other than zero.
    /// </summary>
    /// <param name="condition">How the value is compared.</param>
    /// <param name="threshold">The threshold: a value, or a change in percent.</param>
    /// <param name="current">The value of the latest window.</param>
    /// <param name="previous">The value of the window before it, for a change in percent.</param>
    /// <param name="minimumSampleSize">The fewest samples a window needs to count.</param>
    /// <returns><see langword="true"/> when the threshold is crossed.</returns>
    public static bool IsCrossed(
        AIConversationAlertCondition condition,
        double threshold,
        AIConversationMetricValue current,
        AIConversationMetricValue previous,
        int minimumSampleSize)
    {
        if (current?.Value is not double value || current.SampleSize < Math.Max(0, minimumSampleSize))
        {
            return false;
        }

        switch (condition)
        {
            case AIConversationAlertCondition.Above:
                return value > threshold;

            case AIConversationAlertCondition.Below:
                return value < threshold;
        }

        if (previous?.Value is not double before || before == 0)
        {
            return false;
        }

        var change = (value - before) / Math.Abs(before) * 100;

        return condition == AIConversationAlertCondition.RisesByPercent
            ? change > threshold
            : -change > threshold;
    }
}
