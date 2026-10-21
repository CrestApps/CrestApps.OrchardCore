namespace CrestApps.OrchardCore.Downloads.Models;

/// <summary>
/// How long buyers' download links work, and how often, unless a product says otherwise.
/// </summary>
public sealed class DownloadSettings
{
    /// <summary>
    /// Gets or sets how many times a buyer can download each file. Zero means no limit.
    /// </summary>
    public int DownloadLimit { get; set; } = 5;

    /// <summary>
    /// Gets or sets how many days a buyer's links work. Zero means they never expire.
    /// </summary>
    public int LinkLifetimeDays { get; set; } = 30;
}
