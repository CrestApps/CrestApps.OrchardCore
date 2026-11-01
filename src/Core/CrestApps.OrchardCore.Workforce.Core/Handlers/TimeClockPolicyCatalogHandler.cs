using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using CrestApps.OrchardCore.Workforce.Core.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Handlers;

/// <summary>
/// Stamps and checks time clock policies: a name, pay periods, a rounding step the policy knows, and thresholds that make sense.
/// </summary>
internal sealed class TimeClockPolicyCatalogHandler : CatalogEntryHandlerBase<TimeClockPolicy>
{
    private readonly IClock _clock;
    private readonly PayPeriodCalendarStore _calendarStore;

    internal readonly IStringLocalizer S;

    public TimeClockPolicyCatalogHandler(PayPeriodCalendarStore calendarStore, IClock clock, IStringLocalizer<TimeClockPolicyCatalogHandler> stringLocalizer)
    {
        _calendarStore = calendarStore;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<TimeClockPolicy> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<TimeClockPolicy> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override async Task ValidatingAsync(ValidatingContext<TimeClockPolicy> context, CancellationToken cancellationToken = default)
    {
        var item = context.Model;

        item.Name = item.Name?.Trim();

        if (string.IsNullOrEmpty(item.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(TimeClockPolicy.Name)]));
        }

        if (string.IsNullOrEmpty(item.PayPeriodCalendarId) || await _calendarStore.FindByIdAsync(item.PayPeriodCalendarId, cancellationToken) is null)
        {
            context.Result.Fail(new ValidationResult(S["Choose the pay periods."], [nameof(TimeClockPolicy.PayPeriodCalendarId)]));
        }

        if (item.RoundingMinutes is not (0 or 1 or 5 or 6 or 15))
        {
            context.Result.Fail(new ValidationResult(S["Round to 1, 5, 6 or 15 minutes, or not at all."], [nameof(TimeClockPolicy.RoundingMinutes)]));
        }

        if (item.EarlyGraceMinutes is < 0 or > 120 || item.LateGraceMinutes is < 0 or > 120)
        {
            context.Result.Fail(new ValidationResult(S["Grace is between 0 and 120 minutes."], [nameof(TimeClockPolicy.LateGraceMinutes)]));
        }

        if (item.BlockEarlyPunchInMinutes is < 0 or > 720)
        {
            context.Result.Fail(new ValidationResult(S["Clocking in early is blocked from 0 to 720 minutes before a shift."], [nameof(TimeClockPolicy.BlockEarlyPunchInMinutes)]));
        }

        if (item.AutoMealAfterHours is <= 0 or > 24 || item.AutoMealMinutes is < 0 or > 240)
        {
            context.Result.Fail(new ValidationResult(S["An automatic meal comes after up to 24 hours and lasts up to 240 minutes."], [nameof(TimeClockPolicy.AutoMealAfterHours)]));
        }

        if (item.RequireGeofence && item.GeofenceRadiusMeters is < 10 or > 50000)
        {
            context.Result.Fail(new ValidationResult(S["The site's radius is between 10 and 50,000 meters."], [nameof(TimeClockPolicy.GeofenceRadiusMeters)]));
        }

        if (item.DailyOvertimeHours is <= 0 or > 24 || item.DailyDoubleTimeHours is <= 0 or > 24 || item.WeeklyOvertimeHours is <= 0 or > 168)
        {
            context.Result.Fail(new ValidationResult(S["Overtime starts after more than 0 hours, up to 24 a day or 168 a week."], [nameof(TimeClockPolicy.WeeklyOvertimeHours)]));
        }

        if (item.DailyOvertimeHours.HasValue && item.DailyDoubleTimeHours.HasValue && item.DailyDoubleTimeHours <= item.DailyOvertimeHours)
        {
            context.Result.Fail(new ValidationResult(S["Double time starts after daily overtime."], [nameof(TimeClockPolicy.DailyDoubleTimeHours)]));
        }

        if (item.PointsWindowDays is < 1 or > 3650 || item.AttendancePoints.Any(rule => rule.Points < 0))
        {
            context.Result.Fail(new ValidationResult(S["Points count for 1 to 3,650 days and are never negative."], [nameof(TimeClockPolicy.PointsWindowDays)]));
        }
    }
}
