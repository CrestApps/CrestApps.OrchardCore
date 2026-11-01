using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;

/// <summary>
/// A shift may not last more than a number of hours, breaks included.
/// </summary>
internal sealed class MaxShiftLengthRule : LaborRuleBase
{
    public MaxShiftLengthRule(IStringLocalizer<MaxShiftLengthRule> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Key => LaborRuleKeys.MaxShiftLength;

    public override string DisplayName => S["Longest shift"];

    public override string Description => S["A shift may not last more than a number of hours, breaks included."];

    public override IReadOnlyList<LaborRuleParameter> Parameters =>
        [
            Parameter("Hours", S["Hours"], LaborRuleParameterKind.Hours, "12"),
        ];

    public override Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default)
    {
        var limit = instance.Number("Hours", 12);

        foreach (var shift in context.Candidates)
        {
            var hours = Hours((int)(shift.EndUtc - shift.StartUtc).TotalMinutes);

            if (hours > limit)
            {
                context.Report(this, instance, S["The shift of {0} on {1} lasts {2} hours, more than {3}.", context.Worker.DisplayName, Day(context, shift.Date), Format(hours), Format(limit)], shift.Date, [shift.ShiftKey]);
            }
        }

        return Task.CompletedTask;
    }
}
