using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;

/// <summary>
/// A shift whose template needs skills goes only to a worker who holds them, with a valid certificate where the skill needs one, on its day.
/// </summary>
internal sealed class SkillsRequiredRule : LaborRuleBase
{
    private readonly ISkillAssignmentService _skillAssignments;
    private readonly ISkillManager _skillManager;

    public SkillsRequiredRule(ISkillAssignmentService skillAssignments, ISkillManager skillManager, IStringLocalizer<SkillsRequiredRule> stringLocalizer)
        : base(stringLocalizer)
    {
        _skillAssignments = skillAssignments;
        _skillManager = skillManager;
    }

    public override string Key => LaborRuleKeys.SkillsRequired;

    public override string DisplayName => S["Skills and certificates"];

    public override string Description => S["A shift whose template needs skills goes only to a worker who holds them, with a valid certificate where the skill needs one, on its day."];

    public override async Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default)
    {
        foreach (var shift in context.Candidates.Where(shift => shift.RequiredSkillIds.Count > 0))
        {
            var missing = new List<string>();

            foreach (var skillId in shift.RequiredSkillIds)
            {
                if (!await _skillAssignments.HoldsAsync(SkillSubject.Worker(context.Worker.ItemId), skillId, 0, shift.Date, cancellationToken))
                {
                    missing.Add((await _skillManager.FindByIdAsync(skillId, cancellationToken))?.Name ?? skillId);
                }
            }

            if (missing.Count > 0)
            {
                context.Report(this, instance, S["{0} doesn't hold {1}, needed for the shift on {2}.", context.Worker.DisplayName, string.Join(", ", missing), Day(context, shift.Date)], shift.Date, [shift.ShiftKey]);
            }
        }
    }
}
