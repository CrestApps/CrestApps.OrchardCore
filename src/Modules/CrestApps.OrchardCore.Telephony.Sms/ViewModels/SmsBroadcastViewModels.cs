using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Telephony.Sms.ViewModels;

/// <summary>
/// The create view model for an SMS broadcast.
/// </summary>
public class SmsBroadcastCreateViewModel
{
    /// <summary>
    /// Gets or sets the broadcast name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the sending SMS channel endpoint (DID).
    /// </summary>
    public string EndpointId { get; set; }

    /// <summary>
    /// Gets or sets the message body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the recipient numbers, one per line or comma-separated.
    /// </summary>
    public string RecipientsText { get; set; }

    /// <summary>
    /// Gets or sets the phone numbers selected through the customer picker.
    /// </summary>
    public IList<string> ContactPhones { get; set; } = [];

    /// <summary>
    /// Gets or sets the selectable SMS channel endpoints (DIDs).
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Endpoints { get; set; }
}

/// <summary>
/// The list view model for SMS broadcasts.
/// </summary>
public class SmsBroadcastListViewModel
{
    /// <summary>
    /// Gets or sets the broadcasts, most-recent first.
    /// </summary>
    public IReadOnlyList<SmsBroadcast> Broadcasts { get; set; } = [];
}
