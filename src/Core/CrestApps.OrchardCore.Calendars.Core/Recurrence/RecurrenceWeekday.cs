using NodaTime;

namespace CrestApps.OrchardCore.Calendars.Core.Recurrence;

/// <summary>
/// A <c>BYDAY</c> entry: a day of the week, optionally with an ordinal such as the second Tuesday (<c>2TU</c>) or the last Friday
/// (<c>-1FR</c>) of the month or year.
/// </summary>
/// <param name="Day">The day of the week.</param>
/// <param name="Ordinal">Which one in the month or year, counted from the end when negative, or 0 for every one.</param>
public readonly record struct RecurrenceWeekday(IsoDayOfWeek Day, int Ordinal = 0);
