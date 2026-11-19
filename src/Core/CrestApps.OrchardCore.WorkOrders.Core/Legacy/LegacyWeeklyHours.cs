namespace CrestApps.OrchardCore.WorkOrders.Core.Legacy;

/// <summary>
/// The same opening hours on some days of every week, as a <see cref="LegacyServiceLevelPolicy"/> kept them.
/// </summary>
internal sealed class LegacyWeeklyHours
{
    /// <summary>
    /// Gets or sets the days open.
    /// </summary>
    public List<DayOfWeek> Days { get; set; } = [];

    /// <summary>
    /// Gets or sets when the days open.
    /// </summary>
    public TimeOnly Opens { get; set; } = new(9, 0);

    /// <summary>
    /// Gets or sets when the days close.
    /// </summary>
    public TimeOnly Closes { get; set; } = new(17, 0);
}
