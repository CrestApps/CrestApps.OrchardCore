using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;

/// <summary>
/// Records premium hours when the employer changes a published shift inside the notice window: for an added or moved shift, and a share of the hours of a removed one. Changes a worker asks for owe nothing.
/// </summary>
internal sealed class PredictabilityPayRule : LaborRuleBase
{
    public PredictabilityPayRule(IStringLocalizer<PredictabilityPayRule> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Key => LaborRuleKeys.PredictabilityPay;

    public override string DisplayName => S["Predictability pay"];

    public override string Description => S["Records premium hours when the employer changes a published shift inside the notice window: for an added or moved shift, and a share of the hours of a removed one. Changes a worker asks for owe nothing."];

    public override IReadOnlyList<LaborRuleParameter> Parameters =>
        [
            Parameter("NoticeDays", S["Days of notice"], LaborRuleParameterKind.Days, "14"),
            Parameter("AddedHours", S["Hours for an added shift"], LaborRuleParameterKind.Hours, "1"),
            Parameter("MovedHours", S["Hours for a moved shift"], LaborRuleParameterKind.Hours, "1"),
            Parameter("RemovedPercent", S["Share of a removed shift's hours"], LaborRuleParameterKind.Percent, "50"),
        ];

    public override bool RecordsOnly => true;

    public override Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
