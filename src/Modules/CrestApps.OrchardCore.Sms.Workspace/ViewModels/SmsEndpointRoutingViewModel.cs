using CrestApps.OrchardCore.Sms.Workspace.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Sms.Workspace.ViewModels;

/// <summary>
/// The editor for the SMS routing attached to a channel endpoint.
/// </summary>
public class SmsEndpointRoutingViewModel
{
    /// <summary>
    /// Gets or sets what inbound messages route to: an agent or a queue.
    /// </summary>
    public SmsNumberRouteTargetType TargetType { get; set; }

    /// <summary>
    /// Gets or sets the target identifier (agent profile id or queue id). Empty means unassigned inbox.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets how inbound messages for a queue target are distributed.
    /// </summary>
    public SmsNumberRouteDistributionMode DistributionMode { get; set; }

    /// <summary>
    /// Gets or sets an optional auto-reply message.
    /// </summary>
    public string AutoReplyMessage { get; set; }

    /// <summary>
    /// Gets or sets the selectable target types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> TargetTypes { get; set; }

    /// <summary>
    /// Gets or sets the selectable distribution modes.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> DistributionModes { get; set; }
}
