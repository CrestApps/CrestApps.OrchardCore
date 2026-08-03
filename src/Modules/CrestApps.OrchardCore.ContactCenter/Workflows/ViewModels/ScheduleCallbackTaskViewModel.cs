namespace CrestApps.OrchardCore.ContactCenter.Workflows.ViewModels;

/// <summary>
/// Represents the edit view model for the <c>ScheduleCallbackTask</c> workflow activity.
/// </summary>
public class ScheduleCallbackTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression that resolves the destination number or address to call back.
    /// </summary>
    public string Destination { get; set; }

    /// <summary>
    /// Gets or sets the delay, in minutes from now, before the callback becomes due.
    /// </summary>
    public int DelayMinutes { get; set; }

    /// <summary>
    /// Gets or sets the optional Liquid expression that resolves the campaign the callback belongs to.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the optional Liquid expression that resolves the queue the promoted activity is enqueued into.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the optional Liquid expression that resolves the content item identifier of the contact.
    /// </summary>
    public string ContactContentItemId { get; set; }
}
