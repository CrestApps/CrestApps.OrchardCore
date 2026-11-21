namespace CrestApps.OrchardCore.Surveys.Core.Qti;

/// <summary>
/// A media file of a package that an item's HTML refers to. The HTML refers to it as
/// <see cref="QtiMediaFile.Reference"/> until the import stores it and writes its address.
/// </summary>
public sealed class QtiMediaFile
{
    /// <summary>
    /// The scheme of a reference to a media file in the HTML of an imported question.
    /// </summary>
    public const string Scheme = "qti-media:";

    /// <summary>
    /// The extensions of the images an import takes: the media a package may refer to.
    /// </summary>
    public static readonly IReadOnlySet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp",
    };

    /// <summary>
    /// Tells whether bytes are an image of one of the <see cref="ImageExtensions"/>, by their signature: PNG, JPEG, GIF
    /// or WebP.
    /// </summary>
    /// <param name="content">The bytes.</param>
    /// <returns><see langword="true"/> when they start like an image of those types.</returns>
    public static bool HasImageSignature(ReadOnlySpan<byte> content)
        => content.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
        || content.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF])
        || content.StartsWith("GIF87a"u8)
        || content.StartsWith("GIF89a"u8)
        || (content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8));

    /// <summary>Gets or sets the path of the file in the package, normalized.</summary>
    public string Path { get; set; }

    /// <summary>Gets or sets the bytes of the file.</summary>
    public byte[] Content { get; set; }

    /// <summary>Gets the text that refers to the file in the HTML of the question.</summary>
    public string Reference => Scheme + Path;
}
