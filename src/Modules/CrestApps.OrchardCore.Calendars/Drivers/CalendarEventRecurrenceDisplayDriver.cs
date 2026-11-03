using System.Globalization;
using CrestApps.OrchardCore.Calendars.Core.Recurrence;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.Extensions.Localization;
using NodaTime;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Calendars.Drivers;

/// <summary>
/// The Repeat card of the event editor. Simple choices build the rule; a custom rule covers anything else.
/// </summary>
internal sealed class CalendarEventRecurrenceDisplayDriver : DisplayDriver<CalendarEvent>
{
    private readonly ICalendarService _calendarService;
    private readonly ICalendarTypeManager _typeManager;

    internal readonly IStringLocalizer S;

    public CalendarEventRecurrenceDisplayDriver(
        ICalendarService calendarService,
        ICalendarTypeManager typeManager,
        IStringLocalizer<CalendarEventRecurrenceDisplayDriver> stringLocalizer)
    {
        _calendarService = calendarService;
        _typeManager = typeManager;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(CalendarEvent calendarEvent, BuildEditorContext context)
    {
        if (!await SupportsAsync(calendarEvent))
        {
            return null;
        }

        return Initialize<CalendarEventRecurrenceViewModel>("CalendarEventRecurrence_Edit", model =>
        {
            model.IsOccurrence = calendarEvent.SeriesId is not null;
            Load(calendarEvent, model);
        }).Location("Content:5%Repeat;2");
    }

    public override async Task<IDisplayResult> UpdateAsync(CalendarEvent calendarEvent, UpdateEditorContext context)
    {
        if (!await SupportsAsync(calendarEvent) || calendarEvent.SeriesId is not null)
        {
            return null;
        }

        var model = new CalendarEventRecurrenceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var rule = Build(model, calendarEvent, out var error);

        if (error is not null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CustomRule), error);
        }
        else if (rule is null)
        {
            calendarEvent.Recurrence = null;
        }
        else
        {
            calendarEvent.Recurrence ??= new CalendarRecurrence();
            calendarEvent.Recurrence.Rule = rule;
        }

        return await EditAsync(calendarEvent, context);
    }

    private async Task<bool> SupportsAsync(CalendarEvent calendarEvent)
    {
        var calendar = await _calendarService.FindByIdAsync(calendarEvent.CalendarId);

        return calendar is not null && (_typeManager.FindType(calendar.Type)?.Supports(CalendarCapabilities.Recurrence) ?? true);
    }

    private static void Load(CalendarEvent calendarEvent, CalendarEventRecurrenceViewModel model)
    {
        model.Until = calendarEvent.Start.Date.AddMonths(3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (!CalendarEventExpander.TryGetRule(calendarEvent, out var rule))
        {
            return;
        }

        var simple = rule.ByMonthDay.Count == 0 && rule.ByMonth.Count == 0 && rule.BySetPosition.Count == 0 && !rule.UntilIsUtc;

        model.Frequency = rule.Frequency.ToString().ToUpperInvariant();
        model.Interval = rule.Interval;
        model.WeekDays = rule.ByDay.Where(day => day.Ordinal == 0).Select(day => RecurrenceRule.FormatDay(day.Day)).ToArray();
        model.MonthlyMode = rule.ByDay.Any(day => day.Ordinal != 0) ? "weekday" : "day";
        model.Ends = rule.Count.HasValue ? "after" : rule.Until.HasValue ? "on" : "never";
        model.Count = rule.Count ?? 10;

        if (rule.Until.HasValue)
        {
            model.Until = rule.Until.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (rule.Frequency == RecurrenceFrequency.Weekly && rule.ByDay.Any(day => day.Ordinal != 0))
        {
            simple = false;
        }

        if (rule.Frequency is RecurrenceFrequency.Daily or RecurrenceFrequency.Yearly && rule.ByDay.Count > 0)
        {
            simple = false;
        }

        if (!simple)
        {
            model.CustomRule = rule.ToString();
        }
    }

    private string Build(CalendarEventRecurrenceViewModel model, CalendarEvent calendarEvent, out string error)
    {
        error = null;

        if (!string.IsNullOrWhiteSpace(model.CustomRule))
        {
            if (!RecurrenceRule.TryParse(model.CustomRule, out var custom, out var ruleError))
            {
                error = S["The custom rule is not valid: {0}", ruleError];

                return null;
            }

            return custom.ToString();
        }

        if (string.IsNullOrEmpty(model.Frequency) || !Enum.TryParse<RecurrenceFrequency>(model.Frequency, ignoreCase: true, out var frequency))
        {
            return null;
        }

        var rule = new RecurrenceRule
        {
            Frequency = frequency,
            Interval = Math.Max(1, model.Interval),
        };

        var start = LocalDate.FromDateTime(calendarEvent.Start);

        if (frequency == RecurrenceFrequency.Weekly)
        {
            var days = (model.WeekDays ?? []).Where(day => !string.IsNullOrWhiteSpace(day)).Distinct().ToList();

            if (days.Count == 0)
            {
                days.Add(RecurrenceRule.FormatDay(start.DayOfWeek));
            }

            foreach (var day in days)
            {
                if (RecurrenceRule.TryParse("FREQ=WEEKLY;BYDAY=" + day, out var parsed, out _))
                {
                    rule.ByDay.Add(parsed.ByDay[0]);
                }
            }
        }
        else if (frequency == RecurrenceFrequency.Monthly && model.MonthlyMode == "weekday")
        {
            var week = ((start.Day - 1) / 7) + 1;
            var daysInMonth = CalendarSystem.Iso.GetDaysInMonth(start.Year, start.Month);

            // The last such weekday of the month reads better as "last" than as "fifth".
            rule.ByDay.Add(new RecurrenceWeekday(start.DayOfWeek, start.Day + 7 > daysInMonth ? -1 : week));
        }

        if (model.Ends == "after")
        {
            rule.Count = Math.Clamp(model.Count, 1, 1000);
        }
        else if (model.Ends == "on")
        {
            if (!DateTime.TryParseExact(model.Until, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var until))
            {
                error = S["Enter the date the event stops repeating."];

                return null;
            }

            rule.Until = until;
            rule.UntilIsDate = true;
        }

        return rule.ToString();
    }
}
