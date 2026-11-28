using System.Formats.Tar;
using System.IO.Compression;

namespace CrestApps.OrchardCore.Archives;

/// <summary>
/// The limits an uploaded package must stay within.
/// </summary>
public sealed class PackageArchiveLimits
{
    /// <summary>
    /// Gets or sets the most entries the archive may have, folders included.
    /// </summary>
    public int MaxEntries { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the most bytes the files may take once extracted. The bytes are counted as they are written, so a
    /// size the archive claims for an entry is never trusted.
    /// </summary>
    public long MaxExpandedBytes { get; set; } = 1024L * 1024 * 1024;
}
