namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

/// <summary>
/// One received email, in the same shape whichever source it came from: a provider webhook, a raw MIME post or the
/// address's mailbox. The receiver turns it into a workspace message.
/// </summary>
public sealed class InboundEmail
{
    /// <summary>
    /// Gets or sets the sender.
    /// </summary>
    public InboundEmailAddress From { get; set; }

    /// <summary>
    /// Gets or sets the addresses in the <c>To</c> header.
    /// </summary>
    public IList<InboundEmailAddress> To { get; set; } = [];

    /// <summary>
    /// Gets or sets the addresses in the <c>Cc</c> header.
    /// </summary>
    public IList<InboundEmailAddress> Cc { get; set; } = [];

    /// <summary>
    /// Gets or sets the addresses the email was actually delivered to (the SMTP envelope, <c>Delivered-To</c> or
    /// <c>X-Original-To</c>), which name our address even when it was a Bcc or a forwarding alias.
    /// </summary>
    public IList<string> DeliveredTo { get; set; } = [];

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the plain-text body.
    /// </summary>
    public string TextBody { get; set; }

    /// <summary>
    /// Gets or sets the HTML body.
    /// </summary>
    public string HtmlBody { get; set; }

    /// <summary>
    /// Gets or sets the reply text alone, when the provider already separated it from the quoted history (Postmark and
    /// Mailgun do).
    /// </summary>
    public string StrippedReply { get; set; }

    /// <summary>
    /// Gets or sets the <c>Message-ID</c>, without the angle brackets.
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the <c>In-Reply-To</c>, without the angle brackets.
    /// </summary>
    public string InReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the <c>References</c>, oldest first, without the angle brackets.
    /// </summary>
    public IList<string> References { get; set; } = [];

    /// <summary>
    /// Gets or sets the date the sender's system stamped on the email.
    /// </summary>
    public DateTimeOffset? Date { get; set; }

    /// <summary>
    /// Gets or sets the headers that tell automatic mail from mail a person wrote, keyed by header name.
    /// </summary>
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the attached files.
    /// </summary>
    public IList<InboundEmailAttachment> Attachments { get; set; } = [];

    /// <summary>
    /// Gets or sets the delivery report, when the email is a bounce rather than a message from a person.
    /// </summary>
    public InboundEmailDeliveryReport DeliveryReport { get; set; }
}
