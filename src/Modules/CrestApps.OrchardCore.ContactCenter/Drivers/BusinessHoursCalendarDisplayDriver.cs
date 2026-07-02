using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

internal sealed class BusinessHoursCalendarDisplayDriver : DisplayDriver<BusinessHoursCalendar>
{
    private const int _defaultOpenMinute = 540;
    private const int _defaultCloseMinute = 1020;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursCalendarDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BusinessHoursCalendarDisplayDriver(IStringLocalizer<BusinessHoursCalendarDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
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
        return Initialize<BusinessHoursCalendarViewModel>("BusinessHoursCalendarFields_Edit", model =>
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
        }).Location("Content:1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(BusinessHoursCalendar calendar, UpdateEditorContext context)
    {
        var model = new BusinessHoursCalendarViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Name is a required field."]);
        }

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
