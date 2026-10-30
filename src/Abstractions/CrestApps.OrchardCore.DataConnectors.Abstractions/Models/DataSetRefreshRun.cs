using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.DataConnectors.Models;

/// <summary>
/// One refresh of an imported data set.
/// </summary>
public sealed class DataSetRefreshRun : CatalogItem
{
    /// <summary>
    /// Gets or sets the identifier of the stored document, so a record saved again after a commit updates its row.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the data set refreshed.
    /// </summary>
    public string DataSetId { get; set; }

    /// <summary>
    /// Gets or sets what started the refresh.
    /// </summary>
    public DataSetRefreshTrigger Trigger { get; set; }

    /// <summary>
    /// Gets or sets whether the refresh read every row or only the changed ones.
    /// </summary>
    public DataSetRefreshMode Mode { get; set; }

    /// <summary>
    /// Gets or sets how the refresh ended.
    /// </summary>
    public DataSetRefreshStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the refresh started.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the refresh ended.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of rows read from the source.
    /// </summary>
    public long RowsRead { get; set; }

    /// <summary>
    /// Gets or sets the number of rows the published version holds after the refresh.
    /// </summary>
    public long RowCount { get; set; }

    /// <summary>
    /// Gets or sets the version of rows the refresh wrote.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets a description of how the source's fields changed, or <see langword="null"/> when they didn't.
    /// </summary>
    public string SchemaChanges { get; set; }

    /// <summary>
    /// Gets or sets why the refresh failed, or <see langword="null"/>.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the user name of whoever asked for the refresh, or <see langword="null"/> for a scheduled one.
    /// </summary>
    public string RequestedBy { get; set; }
}

/// <summary>
/// What started a refresh.
/// </summary>
public enum DataSetRefreshTrigger
{
    /// <summary>
    /// Someone asked for it.
    /// </summary>
    Manual,

    /// <summary>
    /// The data set's schedule was due.
    /// </summary>
    Scheduled,
}

/// <summary>
/// How a refresh ended.
/// </summary>
public enum DataSetRefreshStatus
{
    /// <summary>
    /// The refresh is running.
    /// </summary>
    Running,

    /// <summary>
    /// The refresh published a new version of the rows.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The refresh failed; the published rows are kept.
    /// </summary>
    Failed,

    /// <summary>
    /// The refresh was canceled; the published rows are kept.
    /// </summary>
    Canceled,

    /// <summary>
    /// The refresh didn't run, because another refresh of the data set was running.
    /// </summary>
    Skipped,
}
