using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;

/// <summary>
/// Describes the files a messaging channel can carry: which formats, how many per message and how large. The
/// composer offers drag and drop, paste and a file picker for exactly these formats, and the server refuses anything
/// else, so each channel decides for itself. SMS carries pictures only; a channel such as email can list documents too.
/// </summary>
public sealed class MessagingAttachmentCapabilities
{
    /// <summary>
    /// Gets the capabilities of a channel that carries no files.
    /// </summary>
    public static MessagingAttachmentCapabilities None { get; } = new();

    /// <summary>
    /// Gets the file formats the channel accepts; empty when it carries none.
    /// </summary>
    public IReadOnlyList<MessagingFileFormat> Formats { get; init; } = [];

    /// <summary>
    /// Gets the most files one message may carry.
    /// </summary>
    public int MaxCount { get; init; } = 10;

    /// <summary>
    /// Gets the largest total size, in bytes, of the files one message may carry.
    /// </summary>
    public long MaxTotalBytes { get; init; } = 10 * 1024 * 1024;

    /// <summary>
    /// Gets a value indicating whether the composer shrinks a picture that would not fit, rather than refusing it,
    /// as a channel whose carriers cap the message size needs.
    /// </summary>
    public bool ShrinkImagesToFit { get; init; }

    /// <summary>
    /// Gets a value indicating whether the channel carries any files.
    /// </summary>
    public bool IsSupported => Formats.Count > 0 && MaxCount > 0;

    /// <summary>
    /// Gets a value indicating whether every format the channel accepts is a picture.
    /// </summary>
    public bool ImagesOnly => IsSupported && Formats.All(format => format.IsImage);
}
