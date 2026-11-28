namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// Who wrote one message when it was not a person, kept on the message's property bag: an out-of-office or another
/// system's automatic reply that reached the workspace, or the workspace's own auto-reply. A channel's receiver sets it
/// from what its medium says (an email's <c>Auto-Submitted</c> header, for instance), so the workspace marks the message
/// the same way on every channel without knowing any of them.
/// </summary>
public sealed class MessagingMessageOrigin
{
    /// <summary>
    /// Gets or sets a value indicating whether a machine wrote the message rather than a person.
    /// </summary>
    public bool IsAutomatic { get; set; }
}
