namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// The size limits that keep one report run from exhausting the server.
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
}
