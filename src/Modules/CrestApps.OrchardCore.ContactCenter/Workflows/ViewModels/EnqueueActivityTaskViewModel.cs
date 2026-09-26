using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.ViewModels;

/// <summary>
/// Represents the edit view model for the <c>EnqueueActivityTask</c> workflow activity.
/// </summary>
public class EnqueueActivityTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression that resolves the CRM activity identifier to enqueue.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the target queue identifier.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the optional priority override.
    /// </summary>
    public InteractionPriority? Priority { get; set; }

    /// <summary>
    /// Gets or sets the selectable priorities.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Priorities { get; set; }
}
