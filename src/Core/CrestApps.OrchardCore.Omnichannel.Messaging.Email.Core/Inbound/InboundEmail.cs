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
    /// Gets or sets the <c>Reply-To</c> when it names someone other than the sender. It decides whose conversation the
    /// email joins only when one of the business's own addresses sent the email (a form on the business's own site);
    /// otherwise it is kept on the message and never decides who the customer is.
    /// </summary>
    public InboundEmailAddress ReplyTo { get; set; }

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

    /// <summary>
    /// Gets or sets a value indicating whether the system that received the email reported its <c>From</c> domain
    /// authenticated: DMARC passed, or a DKIM signature by that domain did. Set by the source that read the email, from
    /// a report the receiving side wrote and the sender could not (a mailbox host's own <c>Authentication-Results</c>
    /// header, Amazon SES's verdict, SendGrid's DKIM check); <see langword="false"/> when the source reports nothing.
    /// </summary>
    public bool IsFromDomainAuthenticated { get; set; }
}
