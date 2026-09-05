namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

/// <summary>
/// Represents the interaction volume for a single day in the call insights report.
/// </summary>
public sealed class CallInsightsDailyPoint
{
    /// <summary>
    /// Gets or sets the UTC date the volumes are counted for.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the total number of interactions created on the day.
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// Gets or sets the number of interactions that were answered (connected to an agent) on the day.
    /// </summary>
    public long Answered { get; set; }

    /// <summary>
    /// Gets or sets the number of inbound interactions that were abandoned before an agent answered on the day.
    /// </summary>
    public long Abandoned { get; set; }
}
