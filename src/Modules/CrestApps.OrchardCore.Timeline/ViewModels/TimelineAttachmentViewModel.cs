namespace CrestApps.OrchardCore.Timeline.ViewModels;

/// <summary>
/// A file of a timeline entry.
/// </summary>
public class TimelineAttachmentViewModel
{
    /// <summary>
    /// Gets or sets the attachment's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the file's name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the size, in bytes.
    /// </summary>
    public long Length { get; set; }

    /// <summary>
    /// Gets or sets whether it is a picture.
    /// </summary>
    public bool IsImage { get; set; }
}
