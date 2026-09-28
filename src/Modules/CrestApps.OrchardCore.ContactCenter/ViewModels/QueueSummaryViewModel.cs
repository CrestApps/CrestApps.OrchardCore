using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the summary display for an activity queue and its current catalog group.
/// </summary>
public class QueueSummaryViewModel
{
    /// <summary>
    /// Gets or sets the queue being displayed.
    /// </summary>
    public ActivityQueue Queue { get; set; }

    /// <summary>
    /// Gets or sets the current queue-group name.
    /// </summary>
    public string QueueGroupName { get; set; }
}
