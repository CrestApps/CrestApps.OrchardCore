namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Attaches one data set to the data sets before it.
/// </summary>
public sealed class ReportJoinDefinition
{
    /// <summary>
    /// Gets or sets the alias of the data set being attached.
    /// </summary>
    public string Alias { get; set; }

    /// <summary>
    /// Gets or sets how unmatched rows are kept.
    /// </summary>
    public ReportJoinType Type { get; set; }

    /// <summary>
    /// Gets or sets the field pairs that must be equal for two rows to match. Every condition must hold.
    /// </summary>
    public IList<ReportJoinCondition> Conditions { get; set; } = [];
}
