namespace CrestApps.OrchardCore.AI.Agent.Media;

/// <summary>
/// The fields a media tool returns for a file of the media library.
/// </summary>
internal sealed class MediaFileSummary
{
    /// <summary>
    /// Gets or sets the path of the file from the root of the media library. It is the value a media field stores.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Gets or sets the name of the file.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the folder of the file, which is empty for the root folder.
    /// </summary>
    public string Folder { get; set; }

    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the MIME type of the file, from its extension.
    /// </summary>
    public string MimeType { get; set; }

    /// <summary>
    /// Gets or sets the time the file was last changed, in the user's or the site's time zone.
    /// </summary>
    public string LastModified { get; set; }

    /// <summary>
    /// Gets or sets the public address of the file.
    /// </summary>
    public string Url { get; set; }
}
