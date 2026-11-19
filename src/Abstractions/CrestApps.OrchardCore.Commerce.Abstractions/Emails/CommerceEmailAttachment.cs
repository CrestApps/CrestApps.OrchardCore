namespace CrestApps.OrchardCore.Commerce.Emails;

/// <summary>
/// A file sent with a store's email, such as an invoice as a PDF. The sender opens the file only when the email is sent,
/// and disposes the stream it opened afterwards, whether the send succeeded or not.
/// </summary>
public sealed class CommerceEmailAttachment
{
    /// <summary>
    /// Gets the file's name as the recipient sees it, such as <c>INV-0042.pdf</c>: a name only, with no folder. A name
    /// with a path in it stops the email from being sent.
    /// </summary>
    public string FileName { get; init; }

    /// <summary>
    /// Gets the file's media type, such as <c>application/pdf</c>, for a sender or a log that needs it; the site's email
    /// provider may also tell it from the file name.
    /// </summary>
    public string ContentType { get; init; }

    /// <summary>
    /// Gets the function that opens the file's content when the email is sent. The sender disposes the stream.
    /// </summary>
    public Func<CancellationToken, Task<Stream>> OpenAsync { get; init; }
}
