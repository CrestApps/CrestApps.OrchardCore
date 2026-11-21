using System.Xml;
using System.Xml.Linq;
using CrestApps.OrchardCore.Archives;

namespace CrestApps.OrchardCore.Surveys.Core.Qti;

/// <summary>
/// The limits a QTI file must stay within.
/// </summary>
public sealed class QtiReadLimits
{
    /// <summary>
    /// Gets or sets the archive limits: entries, and the bytes read from the package (manifest, items and media).
    /// </summary>
    public PackageArchiveLimits Archive { get; set; } = new()
    {
        MaxEntries = 10_000,
        MaxExpandedBytes = 200L * 1024 * 1024,
    };

    /// <summary>
    /// Gets or sets the most items a package may list.
    /// </summary>
    public int MaxItems { get; set; } = 5_000;

    /// <summary>
    /// Gets or sets the largest media file an item may refer to.
    /// </summary>
    public long MaxMediaBytes { get; set; } = 10L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the most characters an XML file may have.
    /// </summary>
    public long MaxXmlCharacters { get; set; } = 10L * 1024 * 1024;
}
