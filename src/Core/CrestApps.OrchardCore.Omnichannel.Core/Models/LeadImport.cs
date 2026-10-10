namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// One file import a lead arrived in or was updated by. A lead keeps one per import, so the leads of a file can be
/// loaded together even after the file's entry is removed from the import history.
/// </summary>
public sealed class LeadImport
{
    /// <summary>
    /// Gets or sets the identifier of the import entry.
    /// </summary>
    public string EntryId { get; set; }

    /// <summary>
    /// Gets or sets the name of the file that was uploaded, such as <c>July2020.csv</c>.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets when the file was uploaded, in UTC.
    /// </summary>
    public DateTime ImportedUtc { get; set; }
}
