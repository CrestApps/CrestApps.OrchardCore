namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// A stretch of time, in UTC.
/// </summary>
/// <param name="StartUtc">When it starts.</param>
/// <param name="EndUtc">When it ends (exclusive).</param>
public sealed record CalendarTimeWindow(DateTime StartUtc, DateTime EndUtc)
{
    /// <summary>
    /// Gets how long the window lasts.
    /// </summary>
    public TimeSpan Duration => EndUtc - StartUtc;
}
