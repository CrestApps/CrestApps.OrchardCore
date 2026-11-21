namespace CrestApps.OrchardCore.Surveys.Core.Controls.FileUpload;

/// <summary>
/// The settings of a file upload question.
/// </summary>
public sealed class FileUploadSettings
{
    /// <summary>
    /// Gets or sets the most files.
    /// </summary>
    public int MaxFiles { get; set; } = 1;

    /// <summary>
    /// Gets or sets the largest file, in megabytes, or <see langword="null"/> for the site setting.
    /// </summary>
    public int? MaxMegabytes { get; set; }

    /// <summary>
    /// Gets or sets the allowed file extensions, such as .pdf; empty allows what Attachments allows.
    /// </summary>
    public List<string> AllowedFormats { get; set; } = [];
}
