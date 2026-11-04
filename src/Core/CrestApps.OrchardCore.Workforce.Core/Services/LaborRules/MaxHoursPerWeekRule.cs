using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;

/// <summary>
/// A worker may not be paid for more than a number of hours from Monday to Sunday.
/// </summary>
internal sealed class MaxHoursPerWeekRule : LaborRuleBase
{
    public MaxHoursPerWeekRule(IStringLocalizer<MaxHoursPerWeekRule> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Key => LaborRuleKeys.MaxHoursPerWeek;

    public override string DisplayName => S["Most hours a week"];

    public override string Description => S["A worker may not be paid for more than a number of hours from Monday to Sunday."];

    public override IReadOnlyList<LaborRuleParameter> Parameters =>
        [
            Parameter("Hours", S["Hours"], LaborRuleParameterKind.Hours, "40"),
        ];

    public override Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default)
    {
        var limit = instance.Number("Hours", 40);

        foreach (var (week, shifts) in CheckedWeeks(context))
        {
            var hours = Hours(shifts.Sum(shift => shift.PaidMinutes));

            if (hours > limit)
            {
                context.Report(this, instance, S["{0} would work {1} hours in the week of {2}, more than {3}.", context.Worker.DisplayName, Format(hours), Day(context, week), Format(limit)], week, CandidateKeys(shifts));
            }
        }

        return Task.CompletedTask;
    }
}
