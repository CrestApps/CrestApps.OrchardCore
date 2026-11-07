namespace CrestApps.OrchardCore.Outreach.Models;

/// <summary>
/// One recipient of an outreach run, as its audience source pages it.
/// </summary>
public sealed class OutreachRecipient
{
    /// <summary>
    /// Gets or sets the key that identifies the recipient within the run, such as a customer id or a normalized
    /// address. A batch that is retried never sends twice to the same key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the address to send to on the run's channel: an e-mail address, a phone number.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the person behind the recipient (a customer, a contact), for renderers and
    /// policies that read them; optional.
    /// </summary>
    public string ContactId { get; set; }

    /// <summary>
    /// Gets or sets the recipient's name; optional.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets why the source already knows the recipient is not sent to, such as
    /// <see cref="OutreachConstants.SkipReasons.NoConsent"/>; <see langword="null"/> when it is sent to.
    /// </summary>
    public string SkipReason { get; set; }
}
