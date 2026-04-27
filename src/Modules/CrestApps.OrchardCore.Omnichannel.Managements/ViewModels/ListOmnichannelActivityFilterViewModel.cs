using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for list omnichannel activity filter.
/// </summary>
public class ListOmnichannelActivityFilterViewModel
{
    /// <summary>
    /// Gets or sets the urgency level.
    /// </summary>
    public ActivityUrgencyLevel? UrgencyLevel { get; set; }

    /// <summary>
    /// Gets or sets the subject content type.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the attempt filter.
    /// </summary>
    public string AttemptFilter { get; set; }

    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the scheduled from.
    /// </summary>
    public string ScheduledFrom { get; set; }

    /// <summary>
    /// Gets or sets the scheduled to.
    /// </summary>
    public string ScheduledTo { get; set; }

    /// <summary>
    /// Gets or sets the urgency levels.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> UrgencyLevels { get; set; }

    /// <summary>
    /// Gets or sets the subject content types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> SubjectContentTypes { get; set; }

    /// <summary>
    /// Gets or sets the channels.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Channels { get; set; }

    /// <summary>
    /// Gets or sets the attempt filters.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AttemptFilters { get; set; }
}
