using CrestApps.OrchardCore.Telephony.Sms.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Telephony.Sms.ViewModels;

/// <summary>
/// The edit view model for an <c>SmsNumberRoute</c>.
/// </summary>
public class SmsNumberRouteViewModel
{
    /// <summary>
    /// Gets or sets the route name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the bound SMS channel endpoint (DID).
    /// </summary>
    public string EndpointId { get; set; }

    /// <summary>
    /// Gets or sets the target type (Agent or Queue).
    /// </summary>
    public SmsNumberRouteTargetType TargetType { get; set; }

    /// <summary>
    /// Gets or sets the target identifier (agent profile id or queue id).
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
    /// Gets or sets a value indicating whether the route is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the selectable SMS channel endpoints (DIDs).
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Endpoints { get; set; }

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
