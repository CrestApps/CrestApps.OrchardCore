namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The payload of the event raised when an over-dialing campaign queue changes how it paces.
/// </summary>
public sealed class PredictivePacingModeChangedEventData
{
    /// <summary>
    /// Gets or sets the campaign queue.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets how the queue paced before, or <see langword="null"/> on its first cycle.
    /// </summary>
    public PredictivePacingDecisionMode? PreviousMode { get; set; }

    /// <summary>
    /// Gets or sets how the queue paces now.
    /// </summary>
    public PredictivePacingDecisionMode Mode { get; set; }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public PredictivePacingReason Reason { get; set; }
}
