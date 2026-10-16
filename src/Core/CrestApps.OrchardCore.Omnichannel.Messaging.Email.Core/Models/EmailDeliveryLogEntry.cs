namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

/// <summary>
/// One line of an email address's delivery log: an email it sent, or something that happened to one (a bounce, a
/// complaint, a block). Sending limits and sending health are counted from this log, and old lines are pruned.
/// </summary>
public sealed class EmailDeliveryLogEntry
{
    /// <summary>
    /// Gets or sets the entry's identifier.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the sending address, when the event could be traced to one.
    /// </summary>
    public string AddressId { get; set; }

    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public EmailDeliveryEventKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the recipient, normalized.
    /// </summary>
    public string Recipient { get; set; }

    /// <summary>
    /// Gets or sets the recipient's domain, which mailbox provider limits and deferrals are counted by.
    /// </summary>
    public string RecipientDomain { get; set; }

    /// <summary>
    /// Gets or sets the email's Message-ID, or the provider's identifier for it, without angle brackets.
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the email was bulk mail (a broadcast, an opening email, a follow-up).
    /// </summary>
    public bool IsBulk { get; set; }

    /// <summary>
    /// Gets or sets when it happened.
    /// </summary>
    public DateTime OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets the status code and reason the server or provider gave, shortened.
    /// </summary>
    public string Detail { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the provider event this entry records, so a redelivered event is recorded once.
    /// </summary>
    public string EventId { get; set; }
}
