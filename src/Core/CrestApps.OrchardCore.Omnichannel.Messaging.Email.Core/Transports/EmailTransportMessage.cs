namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;

/// <summary>
/// One fully composed email, ready for a transport to deliver: addresses, subject, both bodies, files and the headers
/// that thread it and mark what kind of mail it is.
/// </summary>
public sealed class EmailTransportMessage
{
    /// <summary>
    /// Gets or sets the address the email is sent from.
    /// </summary>
    public string FromAddress { get; set; }

    /// <summary>
    /// Gets or sets the name shown as the sender.
    /// </summary>
    public string FromName { get; set; }

    /// <summary>
    /// Gets or sets the recipient's address.
    /// </summary>
    public string ToAddress { get; set; }

    /// <summary>
    /// Gets or sets the address a reply goes to; the sending address when empty.
    /// </summary>
    public string ReplyToAddress { get; set; }

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
    /// Gets or sets the email's <c>Message-ID</c>, without the angle brackets. A transport that can set headers sends
    /// it, so the reply's <c>In-Reply-To</c> names it and a bounce can be matched to it.
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the <c>Message-ID</c> of the email this one answers, without the angle brackets.
    /// </summary>
    public string InReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the <c>Message-ID</c>s of the thread, oldest first, without the angle brackets.
    /// </summary>
    public IList<string> References { get; set; } = [];

    /// <summary>
    /// Gets or sets further headers, such as <c>Auto-Submitted</c> and <c>List-Unsubscribe</c>, for a transport that
    /// can set them.
    /// </summary>
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the files attached.
    /// </summary>
    public IList<EmailTransportAttachment> Attachments { get; set; } = [];
}
