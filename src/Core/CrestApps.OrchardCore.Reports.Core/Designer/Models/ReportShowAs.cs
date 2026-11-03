namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Identifies how a measure column shows its values: as they are, or as a quick table calculation over the values a
/// table, cross-tab or chart shows. Totals rows always show the plain total.
/// </summary>
public enum ReportShowAs
{
    /// <summary>
    /// Each value is shown as it is.
    /// </summary>
    Value,

    /// <summary>
    /// Each value is shown as its share of the sum of the values it is computed along, as a percentage.
    /// </summary>
    PercentOfTotal,

    /// <summary>
    /// Each value is shown as the sum of the values up to it.
    /// </summary>
    RunningTotal,

    /// <summary>
    /// Each value is shown as its difference from the value before it. The first value has none.
    /// </summary>
    DifferenceFromPrevious,

    /// <summary>
    /// Each value is shown as its change from the value before it, as a percentage of that value. The first value, and
    /// one after a zero, have none.
    /// </summary>
    PercentDifference,

    /// <summary>
    /// Each value is shown as its rank, 1 for the largest. Equal values share a rank.
    /// </summary>
    Rank,
}
