namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// The limits that keep report runs from exhausting the server. A run streams its rows: it reads, joins, filters, groups
/// and sorts them with a bounded amount of memory, writing the rows that don't fit to temporary files, so the memory a
/// run takes doesn't grow with its data.
/// </summary>
public sealed class ReportQueryLimits
{
    /// <summary>
    /// Gets or sets the most rows read from one data set.
    /// </summary>
    public int MaxRowsPerDataSet { get; set; } = 50_000;

    /// <summary>
    /// Gets or sets the most rows the joins may produce.
    /// </summary>
    public int MaxJoinedRows { get; set; } = 250_000;

    /// <summary>
    /// Gets or sets the most rows the result keeps.
    /// </summary>
    public int MaxResultRows { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the most values listed by a drop-down filter.
    /// </summary>
    public int MaxFilterOptions { get; set; } = 500;

    /// <summary>
    /// Gets or sets the most distinct keys a join sends to the data set it joins, in batches of
    /// <see cref="JoinKeyBatchSize"/>, so it reads only the records that can match. With more keys, the joined data set
    /// is read like the others, up to <see cref="MaxRowsPerDataSet"/>.
    /// </summary>
    public int MaxJoinKeys { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets how many join keys one read of a data set carries.
    /// </summary>
    public int JoinKeyBatchSize { get; set; } = 500;

    /// <summary>
    /// Gets or sets how many megabytes of rows one operation of a run, such as a join, a sort or a grouping, keeps in
    /// memory before it writes the others to temporary files.
    /// </summary>
    public int MemoryLimitMegabytes { get; set; } = 16;

    /// <summary>
    /// Gets or sets the directory of the temporary files of the runs, or <see langword="null"/> for the temporary
    /// directory of the system. Each run writes to a directory of its own and deletes it when it ends.
    /// </summary>
    public string TemporaryDirectory { get; set; }

    /// <summary>
    /// Gets or sets how many reports a tenant runs at once; the others wait for one to end. A report that reads a view
    /// runs the view within its own run.
    /// </summary>
    public int MaxConcurrentRuns { get; set; } = 4;

    /// <summary>
    /// Copies the limits, so a run can change its copy.
    /// </summary>
    /// <returns>The copy.</returns>
    public ReportQueryLimits Clone()
    {
        return (ReportQueryLimits)MemberwiseClone();
    }
}
