namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// One pair of fields a join matches on.
/// </summary>
public sealed class ReportJoinCondition
{
    /// <summary>
    /// Gets or sets the qualified key (<c>alias.Field</c>) of a field from a data set listed before the attached one.
    /// </summary>
    public string LeftField { get; set; }

    /// <summary>
    /// Gets or sets the qualified key (<c>alias.Field</c>) of a field from the attached data set.
    /// </summary>
    public string RightField { get; set; }
}
