using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;

/// <summary>
/// Edits the agents who send their messages from a messaging number.
/// </summary>
public class MessagingLineEndpointViewModel
{
    /// <summary>
    /// Gets or sets the users who send from the number. Named apart from the voice card's agents, which post under the same prefix.
    /// </summary>
    public string[] TextingUserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the messaging channels the number can be used for, which decide when the card shows.
    /// </summary>
    [BindNever]
    public string ServedCapabilities { get; set; }

    /// <summary>
    /// Gets or sets the display names of those channels, for the card's wording.
    /// </summary>
    [BindNever]
    public string ChannelNames { get; set; }
}
