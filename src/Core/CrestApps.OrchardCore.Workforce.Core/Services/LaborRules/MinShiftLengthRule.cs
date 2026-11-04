using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;

/// <summary>
/// A shift must last at least a number of hours, such as a minimum call-in.
/// </summary>
internal sealed class MinShiftLengthRule : LaborRuleBase
{
    public MinShiftLengthRule(IStringLocalizer<MinShiftLengthRule> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Key => LaborRuleKeys.MinShiftLength;

    public override string DisplayName => S["Shortest shift"];

    public override string Description => S["A shift must last at least a number of hours, such as a minimum call-in."];

    public override IReadOnlyList<LaborRuleParameter> Parameters =>
        [
            Parameter("Hours", S["Hours"], LaborRuleParameterKind.Hours, "3"),
        ];

    public override Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default)
    {
        var limit = instance.Number("Hours", 3);

        foreach (var shift in context.Candidates)
        {
            var hours = Hours((int)(shift.EndUtc - shift.StartUtc).TotalMinutes);

            if (hours < limit)
            {
                context.Report(this, instance, S["The shift of {0} on {1} lasts {2} hours, fewer than {3}.", context.Worker.DisplayName, Day(context, shift.Date), Format(hours), Format(limit)], shift.Date, [shift.ShiftKey]);
            }
        }

        return Task.CompletedTask;
    }
}
