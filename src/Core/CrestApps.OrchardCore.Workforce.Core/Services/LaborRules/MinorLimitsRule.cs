using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;

/// <summary>
/// A worker under an age may work only so many hours a day and a week, and not after a time of day.
/// </summary>
internal sealed class MinorLimitsRule : LaborRuleBase
{
    public MinorLimitsRule(IStringLocalizer<MinorLimitsRule> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Key => LaborRuleKeys.MinorLimits;

    public override string DisplayName => S["Young workers"];

    public override string Description => S["A worker under an age may work only so many hours a day and a week, and not after a time of day."];

    public override IReadOnlyList<LaborRuleParameter> Parameters =>
        [
            Parameter("UnderAge", S["Applies under the age of"], LaborRuleParameterKind.Years, "18"),
            Parameter("MaxHoursPerDay", S["Most hours a day"], LaborRuleParameterKind.Hours, "8"),
            Parameter("MaxHoursPerWeek", S["Most hours a week"], LaborRuleParameterKind.Hours, "40"),
            Parameter("LatestEnd", S["Latest end"], LaborRuleParameterKind.Time, "22:00"),
        ];

    public override Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default)
    {
        var birth = context.Worker?.DateOfBirth;

        if (!birth.HasValue)
        {
            return Task.CompletedTask;
        }

        var age = (int)instance.Number("UnderAge", 18);
        var dayLimit = instance.Number("MaxHoursPerDay", 8);
        var weekLimit = instance.Number("MaxHoursPerWeek", 40);
        var latest = instance.Time("LatestEnd", new TimeOnly(22, 0));
        var name = context.Worker.DisplayName;

        bool Minor(DateOnly date) => birth.Value.AddYears(age) > date;

        foreach (var shift in context.Candidates.Where(shift => Minor(shift.Date)))
        {
            var end = Calendars.Core.CalendarTime.ToLocal(shift.EndUtc, context.TimeZoneId);

            if (DateOnly.FromDateTime(end) > shift.Date || TimeOnly.FromDateTime(end) > latest)
            {
                context.Report(this, instance, S["{0} is under {1}, so can't work after {2:t}; the shift on {3} ends at {4:t}.", name, age, latest, Day(context, shift.Date), end], shift.Date, [shift.ShiftKey]);
            }
        }

        foreach (var (date, shifts) in CheckedDays(context).Where(day => Minor(day.Date)))
        {
            var hours = Hours(shifts.Sum(shift => shift.PaidMinutes));

            if (hours > dayLimit)
            {
                context.Report(this, instance, S["{0} is under {1}, so can work at most {2} hours on {3}, not {4}.", name, age, Format(dayLimit), Day(context, date), Format(hours)], date, CandidateKeys(shifts));
            }
        }

        foreach (var (week, shifts) in CheckedWeeks(context).Where(item => Minor(item.Week)))
        {
            var hours = Hours(shifts.Sum(shift => shift.PaidMinutes));

            if (hours > weekLimit)
            {
                context.Report(this, instance, S["{0} is under {1}, so can work at most {2} hours in the week of {3}, not {4}.", name, age, Format(weekLimit), Day(context, week), Format(hours)], week, CandidateKeys(shifts));
            }
        }

        return Task.CompletedTask;
    }
}
