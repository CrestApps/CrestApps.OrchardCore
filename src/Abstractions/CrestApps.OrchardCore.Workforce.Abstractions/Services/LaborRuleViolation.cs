using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A broken labor rule.
/// </summary>
public sealed class LaborRuleViolation
{
    /// <summary>
    /// Gets or sets the rule.
    /// </summary>
    public string RuleKey { get; set; }

    /// <summary>
    /// Gets or sets the rule's name.
    /// </summary>
    public string RuleName { get; set; }

    /// <summary>
    /// Gets or sets the rule set.
    /// </summary>
    public string RuleSetId { get; set; }

    /// <summary>
    /// Gets or sets what happens because of it.
    /// </summary>
    public LaborRuleEnforcement Enforcement { get; set; }

    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the worker's name.
    /// </summary>
    public string WorkerName { get; set; }

    /// <summary>
    /// Gets or sets the day it is about.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// Gets or sets the shifts it is about.
    /// </summary>
    public IList<string> ShiftKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets what is wrong.
    /// </summary>
    public string Message { get; set; }
}
