using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;

/// <summary>
/// Edits the agents who send their messages from a messaging number.
/// </summary>
public class MessagingLineEndpointViewModel
{
    /// <summary>
    /// Gets or sets the users who send from the number.
    /// </summary>
    public string[] UserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the messaging channels the number can be used for, which decide when the card shows.
    /// </summary>
    [BindNever]
    public string ServedCapabilities { get; set; }
}
