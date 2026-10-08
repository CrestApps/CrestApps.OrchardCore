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
}
