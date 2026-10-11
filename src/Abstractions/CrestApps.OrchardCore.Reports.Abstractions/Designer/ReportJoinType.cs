namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Identifies which unmatched rows a join keeps.
/// </summary>
public enum ReportJoinType
{
    /// <summary>
    /// Keeps only rows that match on both sides.
    /// </summary>
    Inner,

    /// <summary>
    /// Keeps every row of the data sets before the join, with empty values where the attached data set has no match.
    /// </summary>
    Left,

    /// <summary>
    /// Keeps every row of the attached data set, with empty values where the data sets before the join have no match.
    /// </summary>
    Right,

    /// <summary>
    /// Keeps every row of both sides.
    /// </summary>
    Full,
}
