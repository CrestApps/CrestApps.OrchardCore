namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

/// <summary>
/// Recognises a file of one format from its first bytes.
/// </summary>
/// <param name="content">The start of the file.</param>
/// <returns><see langword="true"/> when the bytes are this format.</returns>
public delegate bool MessagingFileSignature(ReadOnlySpan<byte> content);

/// <summary>
/// One kind of file a messaging channel can carry, such as a JPEG picture or a PDF document. A channel lists the
/// formats it accepts in its capabilities; the composer offers only those, and the server accepts only those.
/// </summary>
public sealed class MessagingFileFormat
{
    private readonly MessagingFileSignature _signature;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingFileFormat"/> class.
    /// </summary>
    /// <param name="name">The name people know the format by, such as <c>JPEG</c>.</param>
    /// <param name="contentType">The media type the format is stored and served as.</param>
    /// <param name="extensions">The file extensions of the format, with their leading dot; the first is the preferred one.</param>
    /// <param name="isImage">Whether the format is a picture the workspace shows inline.</param>
    /// <param name="canShrink">Whether the composer may redraw a picture of this format smaller to fit a size limit.</param>
    /// <param name="signature">
    /// Recognises the format from its bytes, or <see langword="null"/> for a format with no reliable signature (plain
    /// text), which is then recognised by its extension alone and only ever served as a download.
    /// </param>
    public MessagingFileFormat(
        string name,
        string contentType,
        IReadOnlyList<string> extensions,
        bool isImage = false,
        bool canShrink = false,
        MessagingFileSignature signature = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(contentType);
        ArgumentNullException.ThrowIfNull(extensions);

        Name = name;
        ContentType = contentType;
        Extensions = extensions;
        IsImage = isImage;
        CanShrink = canShrink;
        _signature = signature;
    }

    /// <summary>
    /// Gets the name people know the format by, such as <c>JPEG</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the media type the format is stored and served as.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Gets the file extensions of the format, with their leading dot; the first is the preferred one.
    /// </summary>
    public IReadOnlyList<string> Extensions { get; }

    /// <summary>
    /// Gets the extension a file of this format is named with when it has no name of its own.
    /// </summary>
    public string PreferredExtension => Extensions.Count > 0 ? Extensions[0] : string.Empty;

    /// <summary>
    /// Gets a value indicating whether the format is a picture the workspace shows inline. Every other format is only
    /// ever offered as a download.
    /// </summary>
    public bool IsImage { get; }

    /// <summary>
    /// Gets a value indicating whether the composer may redraw a picture of this format smaller to fit a size limit.
    /// An animated format cannot be redrawn without losing its animation.
    /// </summary>
    public bool CanShrink { get; }

    /// <summary>
    /// Gets a value indicating whether the format is recognised from its bytes rather than by its extension alone.
    /// </summary>
    public bool HasSignature => _signature is not null;

    /// <summary>
    /// Determines whether some bytes carry this format's signature.
    /// </summary>
    /// <param name="content">The start of the file.</param>
    /// <returns><see langword="true"/> when the format has a signature and the bytes carry it.</returns>
    public bool MatchesSignature(ReadOnlySpan<byte> content)
        => _signature is not null && _signature(content);

    /// <summary>
    /// Determines whether a file name ends in one of the format's extensions.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns><see langword="true"/> when the extension is the format's.</returns>
    public bool MatchesExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);

        return !string.IsNullOrEmpty(extension) &&
            Extensions.Any(item => string.Equals(item, extension, StringComparison.OrdinalIgnoreCase));
    }
}
