namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

/// <summary>
/// What happened to an email after it left, as the sending server answered or a provider reported later.
/// </summary>
public enum EmailDeliveryEventKind
{
    /// <summary>
    /// The sending server accepted the email. Acceptance is not delivery to the inbox.
    /// </summary>
    Sent = 0,

    /// <summary>
    /// The receiving system reported the email delivered.
    /// </summary>
    Delivered = 1,

    /// <summary>
    /// The receiving system put the email off and will be asked again (a 4xx answer).
    /// </summary>
    Deferred = 2,

    /// <summary>
    /// The email bounced for a reason that may pass: a full mailbox, a server that was down.
    /// </summary>
    SoftBounce = 3,

    /// <summary>
    /// The email bounced because the address does not exist or no longer takes mail. It never will.
    /// </summary>
    HardBounce = 4,

    /// <summary>
    /// The receiving system refused the email because of the sender (a policy or reputation block, a 5.7.x answer).
    /// </summary>
    Blocked = 5,

    /// <summary>
    /// The sending server asked the address to slow down.
    /// </summary>
    Throttled = 6,

    /// <summary>
    /// The recipient marked the email as spam.
    /// </summary>
    Complaint = 7,

    /// <summary>
    /// The recipient unsubscribed through the provider.
    /// </summary>
    Unsubscribed = 8,
}
