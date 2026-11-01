using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Checks a worker taking a shift, through a claim, a trade or a reassignment, against their labor rule set. A rule
/// that blocks or needs an override refuses it; a warning lets it through.
/// </summary>
internal sealed class LaborRuleShiftAssignmentValidator : IShiftAssignmentValidator
{
    private readonly ILaborRuleEvaluator _evaluator;

    internal readonly IStringLocalizer S;

    public LaborRuleShiftAssignmentValidator(ILaborRuleEvaluator evaluator, IStringLocalizer<LaborRuleShiftAssignmentValidator> stringLocalizer)
    {
        _evaluator = evaluator;
        S = stringLocalizer;
    }

    public async Task ValidateAsync(ShiftAssignmentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Worker is null)
        {
            return;
        }

        var result = await _evaluator.EvaluateAsync(new LaborRuleRequest
        {
            Worker = context.Worker,
            TeamId = context.TeamId,
            Candidates =
            [
                new LaborRuleShift
                {
                    ShiftKey = "candidate",
                    TeamId = context.TeamId,
                    Date = context.Date,
                    StartUtc = context.StartUtc,
                    EndUtc = context.EndUtc,
                    PaidMinutes = context.PaidMinutes,
                    BreakMinutes = context.BreakMinutes,
                    RequiredSkillIds = context.RequiredSkillIds,
                    IsCandidate = true,
                },
            ],
            IgnoredShiftKeys = context.IgnoredShiftKeys,
        }, cancellationToken);

        foreach (var violation in result.Violations)
        {
            switch (violation.Enforcement)
            {
                case LaborRuleEnforcement.Block:
                    context.Problems.Add(violation.Message);
                    break;
                case LaborRuleEnforcement.RequireOverride:
                    context.Problems.Add(S["{0} A manager would have to override this rule.", violation.Message]);
                    break;
            }
        }
    }
}
