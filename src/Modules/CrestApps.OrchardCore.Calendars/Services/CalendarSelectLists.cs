using System.Globalization;
using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Builds the select lists the calendar editors share.
/// </summary>
internal static class CalendarSelectLists
{
    /// <summary>
    /// Builds the time zone list, labelled by each zone's standard offset. A saved zone missing from the list stays listed.
    /// </summary>
    /// <param name="clock">The clock, which lists the time zones.</param>
    /// <param name="selected">The selected time zone id.</param>
    /// <returns>The options.</returns>
    public static List<SelectListItem> TimeZones(IClock clock, string selected)
    {
        var options = clock.GetTimeZones()
            .Select(zone => (zone.TimeZoneId, Offset: TimeZoneInfo.TryFindSystemTimeZoneById(zone.TimeZoneId, out var info) ? info.BaseUtcOffset : TimeSpan.Zero))
            .OrderBy(zone => zone.Offset)
            .ThenBy(zone => zone.TimeZoneId, StringComparer.Ordinal)
            .Select(zone => new SelectListItem(
                $"(UTC{(zone.Offset < TimeSpan.Zero ? "-" : "+")}{zone.Offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture)}) {zone.TimeZoneId}",
                zone.TimeZoneId,
                string.Equals(zone.TimeZoneId, selected, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (!string.IsNullOrWhiteSpace(selected) && !options.Any(option => option.Selected))
        {
            options.Insert(0, new SelectListItem(selected, selected, true));
        }

        return options;
    }

    /// <summary>
    /// Builds the calendar visibility list.
    /// </summary>
    /// <param name="S">The string localizer.</param>
    /// <returns>The options.</returns>
    public static List<SelectListItem> Visibilities(IStringLocalizer S)
    {
        return
        [
            new SelectListItem(S["Private: only the owner and the people it is shared with"], nameof(CalendarVisibility.Private)),
            new SelectListItem(S["Shared: everyone sees when it is busy, not the details"], nameof(CalendarVisibility.Shared)),
            new SelectListItem(S["Public: everyone sees its events"], nameof(CalendarVisibility.Public)),
        ];
    }

    /// <summary>
    /// Builds the event status list.
    /// </summary>
    /// <param name="S">The string localizer.</param>
    /// <returns>The options.</returns>
    public static List<SelectListItem> Statuses(IStringLocalizer S)
    {
        return
        [
            new SelectListItem(S["Confirmed"], nameof(CalendarEventStatus.Confirmed)),
            new SelectListItem(S["Tentative"], nameof(CalendarEventStatus.Tentative)),
            new SelectListItem(S["Cancelled"], nameof(CalendarEventStatus.Cancelled)),
        ];
    }

    /// <summary>
    /// Builds the reminder list: none, or one of <see cref="CalendarReminders.Choices"/>.
    /// </summary>
    /// <param name="S">The string localizer.</param>
    /// <param name="selected">The selected value.</param>
    /// <returns>The options.</returns>
    public static List<SelectListItem> Reminders(IStringLocalizer S, string selected)
    {
        var options = new List<SelectListItem> { new(S["No reminder"], string.Empty) };

        foreach (var minutes in CalendarReminders.Choices)
        {
            var text = minutes switch
            {
                0 => S["When it starts"],
                < 60 => S["{0} minutes before", minutes],
                60 => S["1 hour before"],
                < 1440 => S["{0} hours before", minutes / 60],
                _ => S["1 day before"],
            };
            var value = minutes.ToString(System.Globalization.CultureInfo.InvariantCulture);

            options.Add(new SelectListItem(text, value, value == selected));
        }

        return options;
    }

    /// <summary>
    /// Builds the busy/free list.
    /// </summary>
    /// <param name="S">The string localizer.</param>
    /// <returns>The options.</returns>
    public static List<SelectListItem> Transparencies(IStringLocalizer S)
    {
        return
        [
            new SelectListItem(S["Busy"], nameof(CalendarTransparency.Busy)),
            new SelectListItem(S["Free"], nameof(CalendarTransparency.Free)),
        ];
    }

    /// <summary>
    /// Builds the event visibility list.
    /// </summary>
    /// <param name="S">The string localizer.</param>
    /// <returns>The options.</returns>
    public static List<SelectListItem> EventVisibilities(IStringLocalizer S)
    {
        return
        [
            new SelectListItem(S["Same as the calendar"], nameof(CalendarEventVisibility.Default)),
            new SelectListItem(S["Public"], nameof(CalendarEventVisibility.Public)),
            new SelectListItem(S["Private: others see only that the time is busy"], nameof(CalendarEventVisibility.Private)),
        ];
    }
}
