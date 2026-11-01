using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;

/// <summary>
/// Warns when a worker's shifts go past the hours after which time is paid as overtime, in a week or a day.
/// </summary>
internal sealed class OvertimeRule : LaborRuleBase
{
    public OvertimeRule(IStringLocalizer<OvertimeRule> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Key => LaborRuleKeys.Overtime;

    public override string DisplayName => S["Overtime"];

    public override string Description => S["Warns when a worker's shifts go past the hours after which time is paid as overtime, in a week or a day."];

    public override IReadOnlyList<LaborRuleParameter> Parameters =>
        [
            Parameter("WeeklyHours", S["After hours a week"], LaborRuleParameterKind.Hours, "40"),
            Parameter("DailyHours", S["After hours a day (0 for none)"], LaborRuleParameterKind.Hours, "0"),
        ];

    public override Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default)
    {
        var weekly = instance.Number("WeeklyHours", 40);
        var daily = instance.Number("DailyHours", 0);

        foreach (var (week, shifts) in CheckedWeeks(context))
        {
            var hours = Hours(shifts.Sum(shift => shift.PaidMinutes));

            if (weekly > 0 && hours > weekly)
            {
                context.Report(this, instance, S["{0} would work {1} hours of overtime in the week of {2}.", context.Worker.DisplayName, Format(hours - weekly), Day(context, week)], week, CandidateKeys(shifts));
            }
        }

        if (daily > 0)
        {
            foreach (var (date, shifts) in CheckedDays(context))
            {
                var hours = Hours(shifts.Sum(shift => shift.PaidMinutes));

                if (hours > daily)
                {
                    context.Report(this, instance, S["{0} would work {1} hours of overtime on {2}.", context.Worker.DisplayName, Format(hours - daily), Day(context, date)], date, CandidateKeys(shifts));
                }
            }
        }

        return Task.CompletedTask;
    }
}
