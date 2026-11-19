namespace CrestApps.OrchardCore.Attachments.Models;

/// <summary>
/// The upload policy of record attachments, bound from the <c>CrestApps:Attachments</c> configuration section.
/// </summary>
public sealed class AttachmentOptions
{
    /// <summary>
    /// Gets or sets the largest file accepted, in bytes. 20 MB by default.
    /// </summary>
    public long MaxFileBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>
    /// Gets or sets how many files one upload may carry. 10 by default.
    /// </summary>
    public int MaxFilesPerUpload { get; set; } = 10;

    /// <summary>
    /// Gets or sets the names of the formats accepted, such as <c>PDF</c> or <c>JPEG</c>; empty accepts every format
    /// in <see cref="FileFormats.All"/>.
    /// </summary>
    public List<string> AllowedFormats { get; set; } = [];

    /// <summary>
    /// Gets the formats accepted.
    /// </summary>
    /// <returns>The formats.</returns>
    public IReadOnlyList<FileFormat> GetAllowedFormats()
        => AllowedFormats is not { Count: > 0 }
            ? FileFormats.All
            : [.. FileFormats.All.Where(format => AllowedFormats.Contains(format.Name, StringComparer.OrdinalIgnoreCase))];
}
