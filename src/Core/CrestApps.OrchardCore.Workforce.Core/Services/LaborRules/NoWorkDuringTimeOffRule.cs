using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;

/// <summary>
/// A worker can't have a shift during their approved time off.
/// </summary>
internal sealed class NoWorkDuringTimeOffRule : LaborRuleBase
{
    public NoWorkDuringTimeOffRule(IStringLocalizer<NoWorkDuringTimeOffRule> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Key => LaborRuleKeys.NoWorkDuringTimeOff;

    public override string DisplayName => S["No work during time off"];

    public override string Description => S["A worker can't have a shift during their approved time off."];

    public override Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default)
    {
        foreach (var shift in context.Candidates)
        {
            if (context.TimeOff.Any(off => off.StartUtc < shift.EndUtc && off.EndUtc > shift.StartUtc))
            {
                context.Report(this, instance, S["{0} has approved time off during the shift on {1}.", context.Worker.DisplayName, Day(context, shift.Date)], shift.Date, [shift.ShiftKey]);
            }
        }

        return Task.CompletedTask;
    }
}
