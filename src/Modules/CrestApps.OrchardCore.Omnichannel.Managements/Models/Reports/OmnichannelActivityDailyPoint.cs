namespace CrestApps.OrchardCore.Omnichannel.Managements.Models.Reports;

/// <summary>
/// Represents the number of activities created on a single day in the activity summary report.
/// </summary>
public sealed class OmnichannelActivityDailyPoint
{
    /// <summary>
    /// Gets or sets the UTC date the activities were created on.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the number of activities created on the day.
    /// </summary>
    public long Count { get; set; }
}
