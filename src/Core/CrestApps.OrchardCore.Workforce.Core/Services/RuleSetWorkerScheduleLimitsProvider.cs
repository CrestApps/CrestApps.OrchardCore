using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The limits from the worker's labor rule set when one applies: its rest, days in a row and hours rules, each kept
/// within the worker's own hours. Without a rule set, the defaults.
/// </summary>
internal sealed class RuleSetWorkerScheduleLimitsProvider : IWorkerScheduleLimitsProvider
{
    private readonly DefaultWorkerScheduleLimitsProvider _defaults;
    private readonly ILaborRuleEvaluator _evaluator;

    public RuleSetWorkerScheduleLimitsProvider(DefaultWorkerScheduleLimitsProvider defaults, ILaborRuleEvaluator evaluator)
    {
        _defaults = defaults;
        _evaluator = evaluator;
    }

    public async Task<WorkerScheduleLimits> GetAsync(WorkerProfile worker, string teamId, CancellationToken cancellationToken = default)
    {
        var limits = await _defaults.GetAsync(worker, teamId, cancellationToken);
        var set = await _evaluator.ResolveRuleSetAsync(worker, teamId, cancellationToken);

        if (set is null)
        {
            return limits;
        }

        limits.FromRuleSet = true;

        foreach (var rule in set.Rules)
        {
            switch (rule.RuleKey)
            {
                case LaborRuleKeys.MinRest:
                    limits.MinRestHours = rule.Number("Hours", limits.MinRestHours);
                    break;
                case LaborRuleKeys.MaxConsecutiveDays:
                    limits.MaxConsecutiveDays = (int)rule.Number("Days", limits.MaxConsecutiveDays);
                    break;
                case LaborRuleKeys.MaxHoursPerWeek:
                    limits.MaxHoursPerWeek = Lowest(limits.MaxHoursPerWeek, rule.Number("Hours", 40));
                    break;
                case LaborRuleKeys.MaxHoursPerDay:
                    limits.MaxHoursPerDay = Lowest(limits.MaxHoursPerDay, rule.Number("Hours", 10));
                    break;
            }
        }

        return limits;
    }

    private static decimal Lowest(decimal? current, decimal value)
    {
        return current.HasValue ? Math.Min(current.Value, value) : value;
    }
}
