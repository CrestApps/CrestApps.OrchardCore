namespace CrestApps.Core.AI.Chat.Services;

/// <summary>
/// Describes the outcome of a shared post-close processing pass for a chat session.
/// </summary>
public sealed class AIChatSessionPostCloseProcessingResult
{
    public bool HadWork { get; set; }

    public bool IsCompleted { get; set; }

    public bool PostSessionTasksCompletedNow { get; set; }

    public bool AnalyticsRecordedNow { get; set; }

    public bool ConversionGoalsEvaluatedNow { get; set; }
}
