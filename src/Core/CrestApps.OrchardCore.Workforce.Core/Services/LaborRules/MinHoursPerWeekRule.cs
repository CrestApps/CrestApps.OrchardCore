using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;

/// <summary>
/// A worker should be scheduled for at least a number of hours from Monday to Sunday, such as their contract.
/// </summary>
internal sealed class MinHoursPerWeekRule : LaborRuleBase
{
    public MinHoursPerWeekRule(IStringLocalizer<MinHoursPerWeekRule> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Key => LaborRuleKeys.MinHoursPerWeek;

    public override string DisplayName => S["Fewest hours a week"];

    public override string Description => S["A worker should be scheduled for at least a number of hours from Monday to Sunday, such as their contract."];

    public override IReadOnlyList<LaborRuleParameter> Parameters =>
        [
            Parameter("Hours", S["Hours"], LaborRuleParameterKind.Hours, "20"),
        ];

    public override Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default)
    {
        var limit = instance.Number("Hours", 20);

        foreach (var (week, shifts) in CheckedWeeks(context))
        {
            var hours = Hours(shifts.Sum(shift => shift.PaidMinutes));

            if (hours < limit)
            {
                context.Report(this, instance, S["{0} would work only {1} hours in the week of {2}, fewer than {3}.", context.Worker.DisplayName, Format(hours), Day(context, week), Format(limit)], week, CandidateKeys(shifts));
            }
        }

        return Task.CompletedTask;
    }
}
