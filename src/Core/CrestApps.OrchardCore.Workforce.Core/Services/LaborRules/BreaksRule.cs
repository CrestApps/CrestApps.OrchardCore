using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;

/// <summary>
/// A shift longer than a number of hours needs at least a number of minutes of breaks or meals inside it.
/// </summary>
internal sealed class BreaksRule : LaborRuleBase
{
    public BreaksRule(IStringLocalizer<BreaksRule> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Key => LaborRuleKeys.Breaks;

    public override string DisplayName => S["Breaks and meals"];

    public override string Description => S["A shift longer than a number of hours needs at least a number of minutes of breaks or meals inside it."];

    public override IReadOnlyList<LaborRuleParameter> Parameters =>
        [
            Parameter("ShiftHours", S["For shifts longer than (hours)"], LaborRuleParameterKind.Hours, "6"),
            Parameter("BreakMinutes", S["Minutes of breaks or meals"], LaborRuleParameterKind.Minutes, "30"),
        ];

    public override Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default)
    {
        var threshold = instance.Number("ShiftHours", 6);
        var required = (int)instance.Number("BreakMinutes", 30);

        foreach (var shift in context.Candidates)
        {
            var hours = Hours((int)(shift.EndUtc - shift.StartUtc).TotalMinutes);

            if (hours > threshold && shift.BreakMinutes < required)
            {
                context.Report(this, instance, S["The shift of {0} on {1} lasts {2} hours with {3} minutes of breaks; it needs {4}.", context.Worker.DisplayName, Day(context, shift.Date), Format(hours), shift.BreakMinutes, required], shift.Date, [shift.ShiftKey]);
            }
        }

        return Task.CompletedTask;
    }
}
