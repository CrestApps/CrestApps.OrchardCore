using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// What a labor rule checks: a worker, the shifts being checked and the ones around them, the changes being made,
/// approved time off, and the violations found so far.
/// </summary>
public sealed class LaborRuleEvaluationContext
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public WorkerProfile Worker { get; set; }

    /// <summary>
    /// Gets or sets the team the shifts are for.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the time zone the days are in.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the rule set being applied.
    /// </summary>
    public LaborRuleSet RuleSet { get; set; }

    /// <summary>
    /// Gets or sets now, in UTC.
    /// </summary>
    public DateTime NowUtc { get; set; }

    /// <summary>
    /// Gets or sets the worker's shifts: the ones being checked and the ones around them, in start order.
    /// </summary>
    public IReadOnlyList<LaborRuleShift> Shifts { get; set; } = [];

    /// <summary>
    /// Gets the shifts being checked.
    /// </summary>
    public IEnumerable<LaborRuleShift> Candidates => Shifts.Where(shift => shift.IsCandidate);

    /// <summary>
    /// Gets or sets the changes to published shifts being made.
    /// </summary>
    public IReadOnlyList<LaborRuleChange> Changes { get; set; } = [];

    /// <summary>
    /// Gets or sets the worker's approved time off around the shifts.
    /// </summary>
    public IReadOnlyList<(DateTime StartUtc, DateTime EndUtc)> TimeOff { get; set; } = [];

    /// <summary>
    /// Gets the violations found.
    /// </summary>
    public IList<LaborRuleViolation> Violations { get; } = [];

    /// <summary>
    /// Records a violation of a rule.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="instance">The rule's place in the set.</param>
    /// <param name="message">What is wrong.</param>
    /// <param name="date">The day it is about.</param>
    /// <param name="shiftKeys">The shifts it is about.</param>
    public void Report(ILaborRule rule, LaborRuleInstance instance, string message, DateOnly? date, IEnumerable<string> shiftKeys)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(instance);

        Violations.Add(new LaborRuleViolation
        {
            RuleKey = rule.Key,
            RuleName = rule.DisplayName,
            RuleSetId = RuleSet?.ItemId,
            Enforcement = instance.Enforcement,
            WorkerId = Worker?.ItemId,
            WorkerName = Worker?.DisplayName,
            Date = date,
            ShiftKeys = (shiftKeys ?? []).Where(key => !string.IsNullOrEmpty(key)).Distinct(StringComparer.Ordinal).ToList(),
            Message = message,
        });
    }
}
