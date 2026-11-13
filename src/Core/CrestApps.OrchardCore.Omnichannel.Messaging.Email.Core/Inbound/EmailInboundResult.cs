namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

/// <summary>
/// The outcome of receiving one email.
/// </summary>
public sealed class EmailInboundResult
{
    /// <summary>
    /// Gets or sets what became of the email.
    /// </summary>
    public EmailInboundStatus Status { get; set; }

    /// <summary>
    /// Gets or sets why, when the email was not accepted.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the address that received the email, when one did.
    /// </summary>
    public string AddressId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the email's durable inbox record, when it was committed to the inbox.
    /// </summary>
    public string InboxMessageId { get; set; }

    /// <summary>
    /// Creates a result.
    /// </summary>
    /// <param name="status">What became of the email.</param>
    /// <param name="reason">Why, when it was not accepted.</param>
    /// <returns>The result.</returns>
    public static EmailInboundResult Of(EmailInboundStatus status, string reason = null)
        => new() { Status = status, Reason = reason };
}
