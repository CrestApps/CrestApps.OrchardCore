namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// A user as the calendar pickers show it.
/// </summary>
/// <param name="Value">The user id.</param>
/// <param name="Text">The name shown.</param>
public sealed record CalendarUserItem(string Value, string Text);
