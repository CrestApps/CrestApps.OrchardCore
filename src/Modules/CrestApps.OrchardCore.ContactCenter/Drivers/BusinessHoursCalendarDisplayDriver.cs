using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

internal sealed class BusinessHoursCalendarDisplayDriver : DisplayDriver<BusinessHoursCalendar>
{
    private const int _defaultOpenMinute = 540;
    private const int _defaultCloseMinute = 1020;

    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursCalendarDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="clock">The clock, which lists the time zones.</param>
    public BusinessHoursCalendarDisplayDriver(
        IStringLocalizer<BusinessHoursCalendarDisplayDriver> stringLocalizer,
        IClock clock)
    {
        S = stringLocalizer;
        _clock = clock;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(BusinessHoursCalendar calendar, BuildDisplayContext context)
    {
        return CombineAsync(
            View("BusinessHoursCalendar_Fields_SummaryAdmin", calendar)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("BusinessHoursCalendar_Buttons_SummaryAdmin", calendar)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("BusinessHoursCalendar_DefaultMeta_SummaryAdmin", calendar)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(BusinessHoursCalendar calendar, BuildEditorContext context)
    {
        // Grouped in cards: the calendar itself, its week, and its holidays. Every card edits the same model under the
        // same prefix, so the one form still posts all of them together.
        void Populate(BusinessHoursCalendarViewModel model)
        {
            model.Id = calendar.ItemId;
            model.Name = calendar.Name;
            model.Description = calendar.Description;
            model.TimeZoneId = calendar.TimeZoneId;
            model.Enabled = calendar.Enabled;
            model.Days = BuildDays(calendar.WeeklySchedule);
            model.HolidaysText = calendar.Holidays is { Count: > 0 }
                ? string.Join(Environment.NewLine, calendar.Holidays.OrderBy(date => date).Select(date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
                : null;
            model.TimeZoneOptions = BuildTimeZoneOptions(calendar.TimeZoneId);
        }

        return Combine(
            Initialize<BusinessHoursCalendarViewModel>("BusinessHoursCalendarGeneral_Edit", Populate).Location("Content:1%General;1"),
            Initialize<BusinessHoursCalendarViewModel>("BusinessHoursCalendarWeekly_Edit", Populate).Location("Content:1%Weekly schedule;2"),
            Initialize<BusinessHoursCalendarViewModel>("BusinessHoursCalendarHolidays_Edit", Populate).Location("Content:1%Holidays;3"));
    }

    // The IANA time zones, labelled by their standard offset, so nobody has to know a zone's identifier. A zone that is
    // saved but not in the list (typed before the picker existed) stays listed, so saving does not silently clear it.
    private List<SelectListItem> BuildTimeZoneOptions(string selectedTimeZoneId)
    {
        var options = _clock.GetTimeZones()
            .Select(zone => (zone.TimeZoneId, Offset: StandardOffsetOf(zone.TimeZoneId)))
            .OrderBy(zone => zone.Offset)
            .ThenBy(zone => zone.TimeZoneId, StringComparer.Ordinal)
            .Select(zone => new SelectListItem(
                $"(UTC{FormatOffset(zone.Offset)}) {zone.TimeZoneId}",
                zone.TimeZoneId,
                string.Equals(zone.TimeZoneId, selectedTimeZoneId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (!string.IsNullOrWhiteSpace(selectedTimeZoneId) && !options.Any(option => option.Selected))
        {
            options.Insert(0, new SelectListItem(selectedTimeZoneId, selectedTimeZoneId, true));
        }

        return options;
    }

    // The zone's standard (non-daylight) offset, which is how zones are usually listed; zero when the system does not know it.
    private static TimeSpan StandardOffsetOf(string timeZoneId)
        => TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone) ? zone.BaseUtcOffset : TimeSpan.Zero;

    private static string FormatOffset(TimeSpan offset)
        => (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(BusinessHoursCalendar calendar, UpdateEditorContext context)
    {
        var model = new BusinessHoursCalendarViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);


        calendar.Name = model.Name?.Trim();
        calendar.Description = model.Description?.Trim();
        calendar.TimeZoneId = string.IsNullOrWhiteSpace(model.TimeZoneId) ? null : model.TimeZoneId.Trim();
        calendar.Enabled = model.Enabled;
        calendar.WeeklySchedule = BuildSchedule(model.Days);
        calendar.Holidays = ParseHolidays(model.HolidaysText);

        return Edit(calendar, context);
    }

    private static List<BusinessHoursDayViewModel> BuildDays(IList<BusinessHoursDay> schedule)
    {
        var hasSchedule = schedule is { Count: > 0 };
        var days = new List<BusinessHoursDayViewModel>();

        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            var existing = schedule?.FirstOrDefault(entry => entry.Day == day);

            bool isOpen;
            int openMinute;
            int closeMinute;

            if (existing is not null)
            {
                isOpen = existing.IsOpen;
                openMinute = existing.OpenMinute;
                closeMinute = existing.CloseMinute;
            }
            else if (!hasSchedule)
            {
                isOpen = day is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
                openMinute = _defaultOpenMinute;
                closeMinute = _defaultCloseMinute;
            }
            else
            {
                isOpen = false;
                openMinute = _defaultOpenMinute;
                closeMinute = _defaultCloseMinute;
            }

            days.Add(new BusinessHoursDayViewModel
            {
                Day = day,
                DayName = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(day),
                IsOpen = isOpen,
                OpenTime = FormatMinutes(openMinute),
                CloseTime = FormatMinutes(closeMinute),
            });
        }

        return days;
    }

    private static List<BusinessHoursDay> BuildSchedule(IList<BusinessHoursDayViewModel> days)
    {
        var schedule = new List<BusinessHoursDay>();

        if (days is null)
        {
            return schedule;
        }

        foreach (var day in days)
        {
            schedule.Add(new BusinessHoursDay
            {
                Day = day.Day,
                IsOpen = day.IsOpen,
                OpenMinute = ParseMinutes(day.OpenTime, _defaultOpenMinute),
                CloseMinute = ParseMinutes(day.CloseTime, _defaultCloseMinute),
            });
        }

        return schedule;
    }

    private static List<DateOnly> ParseHolidays(string text)
    {
        var holidays = new List<DateOnly>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return holidays;
        }

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                continue;
            }

            if (DateOnly.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && !holidays.Contains(date))
            {
                holidays.Add(date);
            }
        }

        return holidays;
    }

    private static int ParseMinutes(string value, int fallback)
    {
        if (TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return (time.Hour * 60) + time.Minute;
        }

        return fallback;
    }

    private static string FormatMinutes(int minutes)
    {
        var clamped = Math.Clamp(minutes, 0, 1439);

        return $"{clamped / 60:D2}:{clamped % 60:D2}";
    }
}
