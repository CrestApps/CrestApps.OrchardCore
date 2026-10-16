namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

/// <summary>
/// An email address the business no longer sends to: it bounced for good, kept bouncing, or its owner reported the
/// business's mail as spam. It holds for every sending address and every contact that has it.
/// </summary>
public sealed class EmailSuppression
{
    /// <summary>
    /// Gets or sets the record's identifier.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the suppressed address, normalized.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets why it is suppressed.
    /// </summary>
    public EmailSuppressionReason Reason { get; set; }

    /// <summary>
    /// Gets or sets what the server or provider said, shortened.
    /// </summary>
    public string Detail { get; set; }

    /// <summary>
    /// Gets or sets when it was suppressed.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the user who suppressed it by hand, when someone did.
    /// </summary>
    public string CreatedBy { get; set; }
}

/// <summary>
/// Why an email address is suppressed.
/// </summary>
public enum EmailSuppressionReason
{
    /// <summary>
    /// The address does not exist or no longer takes mail.
    /// </summary>
    HardBounce = 0,

    /// <summary>
    /// Mail to the address kept bouncing.
    /// </summary>
    RepeatedSoftBounces = 1,

    /// <summary>
    /// The recipient reported the business's mail as spam.
    /// </summary>
    Complaint = 2,

    /// <summary>
    /// Someone added it to the list by hand.
    /// </summary>
    Manual = 3,
}
