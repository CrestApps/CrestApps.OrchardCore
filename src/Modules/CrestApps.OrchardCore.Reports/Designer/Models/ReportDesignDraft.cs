namespace CrestApps.OrchardCore.Reports.Designer.Models;

/// <summary>
/// The working copy of a designed report: the changes saved automatically while people edit it, which nobody else sees
/// until they are published. There is one per report, kept after publishing so its <see cref="Revision"/> keeps
/// counting: every save, publish, discard, and restore increases it, and a change based on an older revision is
/// refused, so one person never silently overwrites another's work.
/// </summary>
public sealed class ReportDesignDraft
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
    /// Gets or sets the revision, which increases with every change to the report or its draft.
    /// </summary>
    public long Revision { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the draft holds changes that are not published.
    /// </summary>
    public bool HasChanges { get; set; }

    /// <summary>
    /// Gets or sets the unpublished report, or <see langword="null"/> when there are no unpublished changes.
    /// </summary>
    public ReportDesign Design { get; set; }

    /// <summary>
    /// Gets or sets the number of the version restored into the draft, if the draft started from one.
    /// </summary>
    public int? RestoredFrom { get; set; }

    /// <summary>
    /// Gets or sets when the draft last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who last changed the draft.
    /// </summary>
    public string ModifiedById { get; set; }

    /// <summary>
    /// Gets or sets the name of the user who last changed the draft.
    /// </summary>
    public string ModifiedByName { get; set; }
}
