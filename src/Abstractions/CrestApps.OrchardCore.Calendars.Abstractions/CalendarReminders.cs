namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// The reminders an event can have.
/// </summary>
public static class CalendarReminders
{
    /// <summary>
    /// The minutes before the start an event can remind its people: at the start, 5, 10, 15 and 30 minutes, 1 and 2 hours,
    /// and a day before.
    /// </summary>
    public static readonly IReadOnlyList<int> Choices = [0, 5, 10, 15, 30, 60, 120, 1440];

    /// <summary>
    /// Gets the longest reminder, in minutes.
    /// </summary>
    public const int MaxMinutes = 1440;

    /// <summary>
    /// Gets whether a number of minutes is one of the <see cref="Choices"/>.
    /// </summary>
    /// <param name="minutes">The minutes.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public static bool IsChoice(int minutes)
    {
        return Choices.Contains(minutes);
    }
}
