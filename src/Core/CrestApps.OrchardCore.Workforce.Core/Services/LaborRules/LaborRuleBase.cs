using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;

/// <summary>
/// What the built-in labor rules share: their names and the weeks, days and hours they count in.
/// </summary>
internal abstract class LaborRuleBase : ILaborRule
{
    protected LaborRuleBase(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected IStringLocalizer S { get; }

    public abstract string Key { get; }

    public abstract string DisplayName { get; }

    public abstract string Description { get; }

    public virtual IReadOnlyList<LaborRuleParameter> Parameters => [];

    public virtual bool RecordsOnly => false;

    public abstract Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the Monday a day's week starts on.
    /// </summary>
    protected static DateOnly WeekOf(DateOnly date)
    {
        return date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    }

    /// <summary>
    /// Returns the weeks the shifts being checked fall in, with every shift of each week.
    /// </summary>
    protected static IEnumerable<(DateOnly Week, LaborRuleShift[] Shifts)> CheckedWeeks(LaborRuleEvaluationContext context)
    {
        var weeks = context.Candidates.Select(shift => WeekOf(shift.Date)).ToHashSet();

        return context.Shifts
            .GroupBy(shift => WeekOf(shift.Date))
            .Where(group => weeks.Contains(group.Key))
            .OrderBy(group => group.Key)
            .Select(group => (group.Key, group.ToArray()));
    }

    /// <summary>
    /// Returns the days the shifts being checked fall on, with every shift of each day.
    /// </summary>
    protected static IEnumerable<(DateOnly Date, LaborRuleShift[] Shifts)> CheckedDays(LaborRuleEvaluationContext context)
    {
        var days = context.Candidates.Select(shift => shift.Date).ToHashSet();

        return context.Shifts
            .GroupBy(shift => shift.Date)
            .Where(group => days.Contains(group.Key))
            .OrderBy(group => group.Key)
            .Select(group => (group.Key, group.ToArray()));
    }

    protected static IEnumerable<string> CandidateKeys(IEnumerable<LaborRuleShift> shifts)
    {
        return shifts.Where(shift => shift.IsCandidate).Select(shift => shift.ShiftKey);
    }

    protected static decimal Hours(int minutes)
    {
        return Math.Round(minutes / 60m, 2);
    }

    protected static string Format(decimal value)
    {
        return value.ToString("0.##", null);
    }

    protected static string Day(LaborRuleEvaluationContext context, DateOnly date)
    {
        return date.ToString("d", null);
    }

    protected static TimeOnly LocalTime(LaborRuleEvaluationContext context, DateTime utc)
    {
        return TimeOnly.FromDateTime(CalendarTime.ToLocal(utc, context.TimeZoneId));
    }

    protected static LaborRuleParameter Parameter(string name, string label, LaborRuleParameterKind kind, string defaultValue)
    {
        return new LaborRuleParameter(name, label, kind, defaultValue);
    }
}
