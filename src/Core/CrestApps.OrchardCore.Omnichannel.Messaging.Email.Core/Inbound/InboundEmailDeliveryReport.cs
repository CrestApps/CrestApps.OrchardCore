namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

/// <summary>
/// What a bounce (a delivery status notification) says about an email we sent.
/// </summary>
public sealed class InboundEmailDeliveryReport
{
    /// <summary>
    /// Gets or sets the <c>Message-ID</c> of the email that bounced, without the angle brackets, when the report names it.
    /// </summary>
    public string OriginalMessageId { get; set; }

    /// <summary>
    /// Gets or sets the recipient that could not be reached.
    /// </summary>
    public string Recipient { get; set; }

    /// <summary>
    /// Gets or sets the report's action, such as <c>failed</c> or <c>delayed</c>.
    /// </summary>
    public string Action { get; set; }

    /// <summary>
    /// Gets or sets the enhanced status code, such as <c>5.1.1</c>.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the receiving server's own words, such as <c>550 5.1.1 User unknown</c>.
    /// </summary>
    public string Diagnostic { get; set; }

    /// <summary>
    /// Gets a value indicating whether the delivery failed for good (a <c>failed</c> action or a 5.x.x status), rather
    /// than being delayed.
    /// </summary>
    public bool IsPermanentFailure
        => string.Equals(Action, "failed", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(Status) && Status.StartsWith('5'));
}
