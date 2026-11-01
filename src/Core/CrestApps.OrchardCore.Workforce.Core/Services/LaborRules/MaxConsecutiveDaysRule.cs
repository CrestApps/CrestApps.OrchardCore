using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;

/// <summary>
/// A worker may not work more than a number of days in a row.
/// </summary>
internal sealed class MaxConsecutiveDaysRule : LaborRuleBase
{
    public MaxConsecutiveDaysRule(IStringLocalizer<MaxConsecutiveDaysRule> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Key => LaborRuleKeys.MaxConsecutiveDays;

    public override string DisplayName => S["Most days in a row"];

    public override string Description => S["A worker may not work more than a number of days in a row."];

    public override IReadOnlyList<LaborRuleParameter> Parameters =>
        [
            Parameter("Days", S["Days"], LaborRuleParameterKind.Days, "6"),
        ];

    public override Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default)
    {
        var limit = (int)instance.Number("Days", 6);
        var worked = context.Shifts.Select(shift => shift.Date).ToHashSet();
        var reported = new HashSet<DateOnly>();

        foreach (var candidate in context.Candidates.OrderBy(shift => shift.Date))
        {
            var first = candidate.Date;

            while (worked.Contains(first.AddDays(-1)))
            {
                first = first.AddDays(-1);
            }

            var last = candidate.Date;

            while (worked.Contains(last.AddDays(1)))
            {
                last = last.AddDays(1);
            }

            var run = last.DayNumber - first.DayNumber + 1;

            if (run > limit && reported.Add(first))
            {
                context.Report(this, instance, S["{0} would work {1} days in a row from {2}, more than {3}.", context.Worker.DisplayName, run, Day(context, first), limit], first, CandidateKeys(context.Shifts.Where(shift => shift.Date >= first && shift.Date <= last)));
            }
        }

        return Task.CompletedTask;
    }
}
