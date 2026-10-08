using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;

/// <summary>
/// The create view model for a broadcast.
/// </summary>
public class BroadcastCreateViewModel
{
    /// <summary>
    /// Gets or sets the broadcast name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the sending channel endpoint, which decides the channel.
    /// </summary>
    public string EndpointId { get; set; }

    /// <summary>
    /// Gets or sets the message body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the recipient addresses, one per line or comma-separated.
    /// </summary>
    public string RecipientsText { get; set; }

    /// <summary>
    /// Gets or sets the addresses selected through the contact picker.
    /// </summary>
    public IList<string> ContactAddresses { get; set; } = [];

    /// <summary>
    /// Gets or sets the selectable endpoints of every enabled messaging channel.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Endpoints { get; set; }

    /// <summary>
    /// Gets or sets the channel of each endpoint, keyed by endpoint id.
    /// </summary>
    [BindNever]
    public IReadOnlyDictionary<string, string> EndpointChannels { get; set; } = new Dictionary<string, string>();
}

/// <summary>
/// The list view model for broadcasts.
/// </summary>
public class BroadcastListViewModel
{
    /// <summary>
    /// Gets or sets the broadcasts, most-recent first.
    /// </summary>
    public IReadOnlyList<MessagingBroadcast> Broadcasts { get; set; } = [];

    /// <summary>
    /// Gets or sets the enabled channels, keyed by name, to label each broadcast's channel.
    /// </summary>
    public IReadOnlyDictionary<string, ChannelViewModel> Channels { get; set; } = new Dictionary<string, ChannelViewModel>();
}
