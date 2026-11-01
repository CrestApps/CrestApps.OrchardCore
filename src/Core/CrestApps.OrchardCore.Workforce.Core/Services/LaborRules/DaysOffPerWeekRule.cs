using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;

/// <summary>
/// A worker must have at least a number of days with no shift from Monday to Sunday.
/// </summary>
internal sealed class DaysOffPerWeekRule : LaborRuleBase
{
    public DaysOffPerWeekRule(IStringLocalizer<DaysOffPerWeekRule> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Key => LaborRuleKeys.DaysOffPerWeek;

    public override string DisplayName => S["Days off a week"];

    public override string Description => S["A worker must have at least a number of days with no shift from Monday to Sunday."];

    public override IReadOnlyList<LaborRuleParameter> Parameters =>
        [
            Parameter("Days", S["Days"], LaborRuleParameterKind.Days, "1"),
        ];

    public override Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default)
    {
        var limit = (int)instance.Number("Days", 1);

        foreach (var (week, shifts) in CheckedWeeks(context))
        {
            var off = 7 - shifts.Select(shift => shift.Date).Distinct().Count();

            if (off < limit)
            {
                context.Report(this, instance, S["{0} would have only {1} days off in the week of {2}; at least {3} are needed.", context.Worker.DisplayName, off, Day(context, week), limit], week, CandidateKeys(shifts));
            }
        }

        return Task.CompletedTask;
    }
}
