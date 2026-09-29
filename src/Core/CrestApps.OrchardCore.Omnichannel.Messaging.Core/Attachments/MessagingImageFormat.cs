namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

/// <summary>
/// Recognises the picture formats the workspace keeps and shows, from the bytes themselves. What a browser or a
/// provider claims a file is does not count: a file is only stored, and only ever served, as one of these formats
/// when its own header says it is one, so nothing that could run as a page (an SVG or HTML file named .png, for
/// example) is served back from the workspace.
/// </summary>
public static class MessagingImageFormat
{
    /// <summary>
    /// The JPEG media type.
    /// </summary>
    public const string Jpeg = "image/jpeg";

    /// <summary>
    /// The PNG media type.
    /// </summary>
    public const string Png = "image/png";

    /// <summary>
    /// The GIF media type.
    /// </summary>
    public const string Gif = "image/gif";

    /// <summary>
    /// The WebP media type.
    /// </summary>
    public const string WebP = "image/webp";

    /// <summary>
    /// Gets the media types the workspace keeps.
    /// </summary>
    public static IReadOnlyList<string> SupportedContentTypes { get; } = [Jpeg, Png, Gif, WebP];

    /// <summary>
    /// Detects the picture format of some bytes.
    /// </summary>
    /// <param name="content">The start of the file; the first dozen bytes are enough.</param>
    /// <param name="contentType">The detected media type.</param>
    /// <returns><see langword="true"/> when the bytes are a supported picture.</returns>
    public static bool TryDetect(ReadOnlySpan<byte> content, out string contentType)
    {
        contentType = null;

        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            contentType = Jpeg;
        }
        else if (content.Length >= 8 && content[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            contentType = Png;
        }
        else if (content.Length >= 6 && (content[..6].SequenceEqual("GIF87a"u8) || content[..6].SequenceEqual("GIF89a"u8)))
        {
            contentType = Gif;
        }
        else if (content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8))
        {
            contentType = WebP;
        }

        return contentType is not null;
    }

    /// <summary>
    /// Gets the file extension for a supported media type.
    /// </summary>
    /// <param name="contentType">The media type.</param>
    /// <returns>The extension with its leading dot, or an empty string for an unknown type.</returns>
    public static string GetExtension(string contentType)
        => contentType switch
        {
            Jpeg => ".jpg",
            Png => ".png",
            Gif => ".gif",
            WebP => ".webp",
            _ => string.Empty,
        };
}
