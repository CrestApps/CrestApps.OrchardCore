using System.Data;

namespace CrestApps.OrchardCore.ContentTransfer;

public sealed class ContentExportContext : ImportContentContext
{
    public DataRow Row { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this content item should be omitted from the export output.
    /// A handler can set this (for example, to keep only rows that match a contributed filter); the export
    /// writer then skips the row.
    /// </summary>
    public bool Exclude { get; set; }
}
