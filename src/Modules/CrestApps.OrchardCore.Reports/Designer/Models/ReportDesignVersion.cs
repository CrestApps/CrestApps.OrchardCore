namespace CrestApps.OrchardCore.Reports.Designer.Models;

/// <summary>
/// A published state of a designed report, kept so it can be looked at and restored. Versions never change: publishing
/// adds one when the report differs from the latest version.
/// </summary>
public sealed class ReportDesignVersion
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the report.
    /// </summary>
    public string DesignId { get; set; }

    /// <summary>
    /// Gets or sets the version number, starting at 1 for each report.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets the report as it was published.
    /// </summary>
    public ReportDesign Design { get; set; }

    /// <summary>
    /// Gets or sets when the version was published, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who published the version.
    /// </summary>
    public string CreatedById { get; set; }

    /// <summary>
    /// Gets or sets the name of the user who published the version.
    /// </summary>
    public string CreatedByName { get; set; }

    /// <summary>
    /// Gets or sets the number of the version this one was restored from, if any.
    /// </summary>
    public int? RestoredFrom { get; set; }
}
