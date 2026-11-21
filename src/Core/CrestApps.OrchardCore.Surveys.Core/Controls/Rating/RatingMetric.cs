namespace CrestApps.OrchardCore.Surveys.Core.Controls.Rating;

/// <summary>
/// The score a rating question reports in results.
/// </summary>
public enum RatingMetric
{
    /// <summary>
    /// The average only.
    /// </summary>
    None,

    /// <summary>
    /// Net Promoter Score: percent of 9 and 10 minus percent of 0 to 6.
    /// </summary>
    Nps,

    /// <summary>
    /// Customer satisfaction: percent of the two highest values.
    /// </summary>
    Csat,
}
