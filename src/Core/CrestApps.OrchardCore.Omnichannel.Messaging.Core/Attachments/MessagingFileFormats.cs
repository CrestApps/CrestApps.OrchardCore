namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

/// <summary>
/// The file formats the messaging workspace knows, and how a file is matched to one. A channel picks the ones it can
/// carry for its capabilities: SMS carries <see cref="Images"/>, and a channel such as email could add
/// <see cref="Documents"/>.
/// </summary>
/// <remarks>
/// A file is matched by its own bytes wherever the format has a signature, never by the name or type the sender gave
/// it, so nothing that could run as a page (an SVG or HTML file named <c>.png</c>, for example) is ever stored as a
/// picture and served inline. A format with no reliable signature, such as plain text, is matched by its extension
/// and is only ever offered as a download.
/// </remarks>
public static class MessagingFileFormats
{
    private static readonly byte[] _zipSignature = [0x50, 0x4B, 0x03, 0x04];

    /// <summary>
    /// JPEG pictures.
    /// </summary>
    public static readonly MessagingFileFormat Jpeg = new("JPEG", "image/jpeg", [".jpg", ".jpeg"], isImage: true, canShrink: true,
        signature: content => content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF);

    /// <summary>
    /// PNG pictures.
    /// </summary>
    public static readonly MessagingFileFormat Png = new("PNG", "image/png", [".png"], isImage: true, canShrink: true,
        signature: content => content.Length >= 8 && content[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));

    /// <summary>
    /// GIF pictures. They may be animated, so they are never redrawn smaller.
    /// </summary>
    public static readonly MessagingFileFormat Gif = new("GIF", "image/gif", [".gif"], isImage: true,
        signature: content => content.Length >= 6 && (content[..6].SequenceEqual("GIF87a"u8) || content[..6].SequenceEqual("GIF89a"u8)));

    /// <summary>
    /// WebP pictures.
    /// </summary>
    public static readonly MessagingFileFormat WebP = new("WebP", "image/webp", [".webp"], isImage: true, canShrink: true,
        signature: content => content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8));

    /// <summary>
    /// PDF documents.
    /// </summary>
    public static readonly MessagingFileFormat Pdf = new("PDF", "application/pdf", [".pdf"],
        signature: content => content.Length >= 5 && content[..5].SequenceEqual("%PDF-"u8));

    /// <summary>
    /// Word documents.
    /// </summary>
    public static readonly MessagingFileFormat Word = new("Word", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", [".docx"],
        signature: IsZip);

    /// <summary>
    /// Excel workbooks.
    /// </summary>
    public static readonly MessagingFileFormat Excel = new("Excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [".xlsx"],
        signature: IsZip);

    /// <summary>
    /// PowerPoint presentations.
    /// </summary>
    public static readonly MessagingFileFormat PowerPoint = new("PowerPoint", "application/vnd.openxmlformats-officedocument.presentationml.presentation", [".pptx"],
        signature: IsZip);

    /// <summary>
    /// Plain text files.
    /// </summary>
    public static readonly MessagingFileFormat Text = new("Text", "text/plain", [".txt"]);

    /// <summary>
    /// Comma-separated values.
    /// </summary>
    public static readonly MessagingFileFormat Csv = new("CSV", "text/csv", [".csv"]);

    /// <summary>
    /// Contact cards.
    /// </summary>
    public static readonly MessagingFileFormat VCard = new("vCard", "text/vcard", [".vcf", ".vcard"]);

    /// <summary>
    /// Gets the picture formats: JPEG, PNG, GIF and WebP.
    /// </summary>
    public static IReadOnlyList<MessagingFileFormat> Images { get; } = [Jpeg, Png, Gif, WebP];

    /// <summary>
    /// Gets the common document formats: PDF, Word, Excel, PowerPoint, text, CSV and contact cards.
    /// </summary>
    public static IReadOnlyList<MessagingFileFormat> Documents { get; } = [Pdf, Word, Excel, PowerPoint, Text, Csv, VCard];

    /// <summary>
    /// Gets every format the workspace knows.
    /// </summary>
    public static IReadOnlyList<MessagingFileFormat> All { get; } = [.. Images, .. Documents];

    /// <summary>
    /// Finds a known format by its media type.
    /// </summary>
    /// <param name="contentType">The media type.</param>
    /// <returns>The format, or <see langword="null"/> when the type is not one the workspace knows.</returns>
    public static MessagingFileFormat FindByContentType(string contentType)
        => string.IsNullOrEmpty(contentType)
            ? null
            : All.FirstOrDefault(format => string.Equals(format.ContentType, contentType, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Matches a file to one of the allowed formats.
    /// </summary>
    /// <param name="content">The file's bytes; the first dozen are enough for a format with a signature.</param>
    /// <param name="fileName">The file's name, when known. It decides between formats that share a signature, and
    /// is what a format with no signature is recognised by.</param>
    /// <param name="declaredContentType">The type the sender declared, used only for a format with no signature and
    /// only when the file has no usable name.</param>
    /// <param name="allowed">The formats the channel accepts.</param>
    /// <returns>The matching format, or <see langword="null"/> when the file is none of them.</returns>
    public static MessagingFileFormat Detect(ReadOnlySpan<byte> content, string fileName, string declaredContentType, IEnumerable<MessagingFileFormat> allowed)
    {
        var formats = allowed?.Where(format => format is not null).ToArray() ?? [];

        if (formats.Length == 0 || content.IsEmpty)
        {
            return null;
        }

        // A file whose bytes carry a known signature is that format and nothing else. Formats that share one (the
        // Office formats are all ZIP files) are told apart by the extension.
        var signed = new List<MessagingFileFormat>();

        foreach (var format in All)
        {
            if (format.MatchesSignature(content))
            {
                signed.Add(format);
            }
        }

        if (signed.Count > 0)
        {
            var candidates = signed.Where(format => formats.Contains(format)).ToArray();

            if (candidates.Length == 1 && (signed.Count == 1 || candidates[0].MatchesExtension(fileName)))
            {
                return candidates[0];
            }

            return candidates.FirstOrDefault(format => format.MatchesExtension(fileName));
        }

        // Only a file with no known signature can be one of the formats that has none.
        var unsigned = formats.Where(format => !format.HasSignature).ToArray();

        return unsigned.FirstOrDefault(format => format.MatchesExtension(fileName)) ??
            (string.IsNullOrEmpty(Path.GetExtension(fileName ?? string.Empty))
                ? unsigned.FirstOrDefault(format => string.Equals(format.ContentType, declaredContentType?.Split(';')[0].Trim(), StringComparison.OrdinalIgnoreCase))
                : null);
    }

    private static bool IsZip(ReadOnlySpan<byte> content)
        => content.Length >= 4 && content[..4].SequenceEqual(_zipSignature);
}
