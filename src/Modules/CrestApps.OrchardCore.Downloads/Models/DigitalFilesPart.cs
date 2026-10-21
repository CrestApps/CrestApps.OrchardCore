using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Downloads.Models;

/// <summary>
/// The files a buyer of the product gets. They are kept in the private file store, never in the media library, so
/// nobody reaches them except through a buyer's download link.
/// </summary>
public sealed class DigitalFilesPart : ContentPart
{
    public List<DigitalFile> Files { get; set; } = [];

    /// <summary>
    /// Gets or sets how many times a buyer can download each file, or <see langword="null"/> to use the site's setting.
    /// Zero means no limit.
    /// </summary>
    public int? DownloadLimit { get; set; }

    /// <summary>
    /// Gets or sets how many days a buyer's links work, or <see langword="null"/> to use the site's setting. Zero means
    /// they never expire.
    /// </summary>
    public int? LinkLifetimeDays { get; set; }
}

/// <summary>
/// A file in the private store.
/// </summary>
public sealed class DigitalFile
{
    public string FileId { get; set; }

    public string FileName { get; set; }

    public long Size { get; set; }
}
