using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the editor of a time clock policy, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class TimeClockPolicyEditDisplayDriver : DisplayDriver<TimeClockPolicyEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeClockPolicyEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeClockPolicyEditDisplayDriver(IStringLocalizer<TimeClockPolicyEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(TimeClockPolicyEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<TimeClockPolicyEditViewModel>("TimeClockPolicyDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<TimeClockPolicyEditViewModel>("TimeClockPolicyRounding_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Rounding And Grace"], "20")),
            Initialize<TimeClockPolicyEditViewModel>("TimeClockPolicyClocking_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Clocking In"], "30")),
            Initialize<TimeClockPolicyEditViewModel>("TimeClockPolicyMeals_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "4").Card(S["Meals"], "40")),
            Initialize<TimeClockPolicyEditViewModel>("TimeClockPolicyOvertime_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "5").Card(S["Overtime"], "50")),
            Initialize<TimeClockPolicyEditViewModel>("TimeClockPolicyHolidays_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "6").Card(S["Holidays And Weekends"], "60")),
            Initialize<TimeClockPolicyEditViewModel>("TimeClockPolicyPoints_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "7").Card(S["Attendance Points"], "70")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(TimeClockPolicyEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.Description, m => m.PayPeriodCalendarId, m => m.Enabled, m => m.RoundingMinutes, m => m.RoundingDirection, m => m.EarlyGraceMinutes, m => m.LateGraceMinutes, m => m.BlockEarlyPunchInMinutes, m => m.RequireGeofence, m => m.GeofenceRadiusMeters, m => m.MissedPunchHandling, m => m.AutoMealAfterHours, m => m.AutoMealMinutes, m => m.LongMealMinutes, m => m.WeeklyOvertimeHours, m => m.DailyOvertimeHours, m => m.DailyDoubleTimeHours, m => m.SeventhDayOvertime, m => m.Holidays, m => m.PointsWindowDays, m => m.PointsThreshold);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(TimeClockPolicyEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(TimeClockPolicyEditViewModel source, TimeClockPolicyEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.Description = source.Description;
        model.PayPeriodCalendarId = source.PayPeriodCalendarId;
        model.RoundingMinutes = source.RoundingMinutes;
        model.RoundingDirection = source.RoundingDirection;
        model.EarlyGraceMinutes = source.EarlyGraceMinutes;
        model.LateGraceMinutes = source.LateGraceMinutes;
        model.BlockEarlyPunchInMinutes = source.BlockEarlyPunchInMinutes;
        model.AutoMealAfterHours = source.AutoMealAfterHours;
        model.AutoMealMinutes = source.AutoMealMinutes;
        model.LongMealMinutes = source.LongMealMinutes;
        model.AllowedSources = source.AllowedSources;
        model.AdditionalSources = source.AdditionalSources;
        model.RequireGeofence = source.RequireGeofence;
        model.GeofenceRadiusMeters = source.GeofenceRadiusMeters;
        model.MissedPunchHandling = source.MissedPunchHandling;
        model.DailyOvertimeHours = source.DailyOvertimeHours;
        model.DailyDoubleTimeHours = source.DailyDoubleTimeHours;
        model.WeeklyOvertimeHours = source.WeeklyOvertimeHours;
        model.SeventhDayOvertime = source.SeventhDayOvertime;
        model.Holidays = source.Holidays;
        model.WeekendDays = source.WeekendDays;
        model.Points = source.Points;
        model.PointsWindowDays = source.PointsWindowDays;
        model.PointsThreshold = source.PointsThreshold;
        model.Enabled = source.Enabled;
        model.CalendarOptions = source.CalendarOptions;
    }
}
