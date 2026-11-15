namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// The kinds of <see cref="DocumentEvent"/> the Invoicing modules record.
/// </summary>
public static class DocumentEventKinds
{
    /// <summary>
    /// The draft was created.
    /// </summary>
    public const string Created = "created";

    /// <summary>
    /// The document was issued: numbered, locked and posted.
    /// </summary>
    public const string Issued = "issued";

    /// <summary>
    /// The document was sent, or a send failed.
    /// </summary>
    public const string Sent = "sent";

    /// <summary>
    /// The customer opened the document for the first time.
    /// </summary>
    public const string Viewed = "viewed";

    /// <summary>
    /// A payment was applied.
    /// </summary>
    public const string Payment = "payment";

    /// <summary>
    /// A credit note reduced the document.
    /// </summary>
    public const string Credited = "credited";

    /// <summary>
    /// Money was given back.
    /// </summary>
    public const string Refunded = "refunded";

    /// <summary>
    /// The document was voided.
    /// </summary>
    public const string Voided = "voided";

    /// <summary>
    /// The document was written off as uncollectible, or reinstated.
    /// </summary>
    public const string WrittenOff = "written-off";

    /// <summary>
    /// A note was added.
    /// </summary>
    public const string Note = "note";

    /// <summary>
    /// The customer accepted an estimate.
    /// </summary>
    public const string Accepted = "accepted";

    /// <summary>
    /// The customer declined an estimate.
    /// </summary>
    public const string Declined = "declined";

    /// <summary>
    /// An estimate expired.
    /// </summary>
    public const string Expired = "expired";

    /// <summary>
    /// An estimate was withdrawn or replaced by a revision.
    /// </summary>
    public const string Canceled = "canceled";

    /// <summary>
    /// A document was turned into another, such as an estimate into an invoice.
    /// </summary>
    public const string Converted = "converted";
}
