namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

/// <summary>
/// What became of a received email.
/// </summary>
public enum EmailInboundStatus
{
    /// <summary>
    /// The email was committed to the durable inbox and handed to the workspace and automation.
    /// </summary>
    Accepted,

    /// <summary>
    /// The email was received before; it was not recorded again.
    /// </summary>
    Duplicate,

    /// <summary>
    /// The email was deliberately not received: a bounce (applied to the email it bounced), mail from one of our own
    /// addresses, or mail with no sender.
    /// </summary>
    Ignored,

    /// <summary>
    /// None of the email's recipients is one of the business's email addresses.
    /// </summary>
    UnknownAddress,

    /// <summary>
    /// The same email is being received right now by another call; this one should be retried.
    /// </summary>
    Busy,
}
