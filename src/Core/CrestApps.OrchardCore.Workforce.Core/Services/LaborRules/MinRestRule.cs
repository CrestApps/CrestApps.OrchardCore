using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;

/// <summary>
/// A worker must rest at least a number of hours between the end of one shift and the start of the next.
/// </summary>
internal sealed class MinRestRule : LaborRuleBase
{
    public MinRestRule(IStringLocalizer<MinRestRule> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Key => LaborRuleKeys.MinRest;

    public override string DisplayName => S["Rest between shifts"];

    public override string Description => S["A worker must rest at least a number of hours between the end of one shift and the start of the next."];

    public override IReadOnlyList<LaborRuleParameter> Parameters =>
        [
            Parameter("Hours", S["Hours"], LaborRuleParameterKind.Hours, "10"),
        ];

    public override Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default)
    {
        var limit = instance.Number("Hours", 10);
        var ordered = context.Shifts.OrderBy(shift => shift.StartUtc).ToArray();

        for (var index = 1; index < ordered.Length; index++)
        {
            var previous = ordered[index - 1];
            var next = ordered[index];

            if (!previous.IsCandidate && !next.IsCandidate)
            {
                continue;
            }

            var gap = Hours((int)(next.StartUtc - previous.EndUtc).TotalMinutes);

            if (gap < limit)
            {
                context.Report(this, instance, S["{0} would rest only {1} hours before the shift on {2}; at least {3} are needed.", context.Worker.DisplayName, Format(Math.Max(0, gap)), Day(context, next.Date), Format(limit)], next.Date, [previous.ShiftKey, next.ShiftKey]);
            }
        }

        return Task.CompletedTask;
    }
}
