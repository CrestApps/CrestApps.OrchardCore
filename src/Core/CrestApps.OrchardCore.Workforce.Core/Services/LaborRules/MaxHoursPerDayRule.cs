using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;

/// <summary>
/// A worker may not be paid for more than a number of hours in a day.
/// </summary>
internal sealed class MaxHoursPerDayRule : LaborRuleBase
{
    public MaxHoursPerDayRule(IStringLocalizer<MaxHoursPerDayRule> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Key => LaborRuleKeys.MaxHoursPerDay;

    public override string DisplayName => S["Most hours a day"];

    public override string Description => S["A worker may not be paid for more than a number of hours in a day."];

    public override IReadOnlyList<LaborRuleParameter> Parameters =>
        [
            Parameter("Hours", S["Hours"], LaborRuleParameterKind.Hours, "10"),
        ];

    public override Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default)
    {
        var limit = instance.Number("Hours", 10);

        foreach (var (date, shifts) in CheckedDays(context))
        {
            var hours = Hours(shifts.Sum(shift => shift.PaidMinutes));

            if (hours > limit)
            {
                context.Report(this, instance, S["{0} would work {1} hours on {2}, more than {3}.", context.Worker.DisplayName, Format(hours), Day(context, date), Format(limit)], date, CandidateKeys(shifts));
            }
        }

        return Task.CompletedTask;
    }
}
