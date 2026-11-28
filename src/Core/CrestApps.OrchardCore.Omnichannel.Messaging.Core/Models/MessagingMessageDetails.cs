namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// The parts of one message the workspace shows beyond its body, kept on the message's property bag: the subject
/// line on a channel whose messages carry one, and the history a reply quoted. A channel without subjects or quoted
/// replies, such as SMS, never sets them.
/// </summary>
public sealed class MessagingMessageDetails
{
    /// <summary>
    /// Gets or sets the subject line of the message.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the earlier messages the sender quoted below the reply, separated from the reply itself so the
    /// thread shows what is new and folds the rest away.
    /// </summary>
    public string QuotedText { get; set; }
}
