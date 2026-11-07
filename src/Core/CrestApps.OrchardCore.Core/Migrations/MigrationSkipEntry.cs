namespace CrestApps.OrchardCore.Core.Migrations;

/// <summary>
/// The summary of one data migration run that skipped records an administrator must look at.
/// </summary>
public sealed class MigrationSkipEntry
{
    /// <summary>
    /// Gets or sets the name of the migration step.
    /// </summary>
    public string MigrationName { get; set; }

    /// <summary>
    /// Gets or sets how many records were skipped.
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// Gets or sets the identifiers of the first skipped records, at most <see cref="MigrationSkipReport.MaxListedIds"/>.
    /// </summary>
    public List<string> SkippedIds { get; set; } = [];

    /// <summary>
    /// Gets or sets when the migration recorded the skips.
    /// </summary>
    public DateTime RecordedUtc { get; set; }
}
