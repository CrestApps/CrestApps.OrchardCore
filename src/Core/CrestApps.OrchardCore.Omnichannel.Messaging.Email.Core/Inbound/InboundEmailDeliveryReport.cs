namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

/// <summary>
/// What a bounce (a delivery status notification) says about an email we sent.
/// </summary>
public sealed class InboundEmailDeliveryReport
{
    /// <summary>
    /// Gets or sets the type of report the email said it is, such as <c>delivery-status</c> for a bounce or
    /// <c>disposition-notification</c> for a read receipt; <see langword="null"/> when it named none.
    /// </summary>
    public string ReportType { get; set; }

    /// <summary>
    /// Gets or sets the <c>Message-ID</c> of the email that bounced, without the angle brackets, when the report names it.
    /// </summary>
    public string OriginalMessageId { get; set; }

    /// <summary>
    /// Gets or sets the recipient that could not be reached.
    /// </summary>
    public string Recipient { get; set; }

    /// <summary>
    /// Gets or sets the report's action: <c>failed</c> or <c>delayed</c>, which are the ones acted on, or <c>delivered</c>,
    /// <c>relayed</c> or <c>expanded</c>, which say the email went through.
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
    /// Gets a value indicating whether the report is a delivery status notification, rather than a read receipt or
    /// another kind of report. A report that names no type is taken for one, as only a delivery status part gives it an
    /// action.
    /// </summary>
    public bool IsDeliveryStatus
        => string.IsNullOrEmpty(ReportType) ||
            string.Equals(ReportType, "delivery-status", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ReportType, "global-delivery-status", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a value indicating whether the delivery failed for good: a <c>failed</c> action whose status is not a
    /// temporary 4.x.x one. A failure with a temporary status is the sending server giving up on a problem that may pass,
    /// such as a full mailbox, and a delayed or successful delivery has not failed at all.
    /// </summary>
    public bool IsPermanentFailure
        => string.Equals(Action, "failed", StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrEmpty(Status) || !Status.StartsWith('4'));
}
