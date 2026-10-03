namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The agents who send their messages from a messaging number, such as an SMS number, stored in the number's
/// properties. It is the messaging counterpart of <see cref="OutboundLineSettings"/>, the agents who dial from a
/// phone number.
/// </summary>
public sealed class MessagingLineSettings
{
    /// <summary>
    /// Gets or sets the identifiers of the users who send from this number.
    /// </summary>
    public IList<string> UserIds { get; set; } = [];
}
