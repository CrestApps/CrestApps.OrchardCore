using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the list omnichannel activity filter.
/// </summary>
public sealed class ListOmnichannelActivityFilter : Entity
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
    /// Gets or sets the channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the attempt filter.
    /// </summary>
    public string AttemptFilter { get; set; }

    /// <summary>
    /// Gets or sets the scheduled from.
    /// </summary>
    public DateTime? ScheduledFrom { get; set; }

    /// <summary>
    /// Gets or sets the scheduled to.
    /// </summary>
    public DateTime? ScheduledTo { get; set; }

    /// <summary>
    /// Gets or sets the route values.
    /// </summary>
    [BindNever]
    public RouteValueDictionary RouteValues { get; set; } = [];
}
