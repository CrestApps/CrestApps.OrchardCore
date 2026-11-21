using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Refuses responses on days and at hours the weekly schedule does not allow, in the site's time zone.
/// </summary>
public sealed class WeeklyScheduleRule : ISurveyAccessRule
{
    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeeklyScheduleRule"/> class.
    /// </summary>
    /// <param name="localClock">The local clock.</param>
    public WeeklyScheduleRule(ILocalClock localClock)
    {
        _localClock = localClock;
    }

    /// <inheritdoc />
    public string Name => "WeeklySchedule";

    /// <inheritdoc />
    public int Order => SurveyAccessRuleOrder.Schedule;

    /// <inheritdoc />
    public async Task<SurveyAccessDecision> CheckStartAsync(SurveyAccessContext context)
    {
        if (context.IsTest || context.Survey.Access?.Schedule?.Days is not { Count: > 0 } days)
        {
            return SurveyAccessDecision.Allow;
        }

        var local = await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(context.UtcNow, DateTimeKind.Utc)));

        return IsAllowed(days, local.DateTime)
            ? SurveyAccessDecision.Allow
            : SurveyAccessDecision.Deny(SurveyErrorCode.NotAvailableNow);
    }

    /// <summary>
    /// Gets whether a local time is allowed by a weekly schedule.
    /// </summary>
    /// <param name="days">The rule of each day.</param>
    /// <param name="local">The local date and time.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public static bool IsAllowed(IReadOnlyDictionary<DayOfWeek, DaySchedule> days, DateTime local)
    {
        if (days is null || !days.TryGetValue(local.DayOfWeek, out var day) || day is null)
        {
            return true;
        }

        return day.Mode switch
        {
            DayScheduleMode.PreventAll => false,
            DayScheduleMode.Between => InRange(TimeOnly.FromDateTime(local), day.From ?? TimeOnly.MinValue, day.To ?? TimeOnly.MaxValue),
            _ => true,
        };
    }

    private static bool InRange(TimeOnly time, TimeOnly from, TimeOnly to)
        => from <= to ? time >= from && time < to : time >= from || time < to;
}
