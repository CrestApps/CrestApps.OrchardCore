using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// What checking shifts against labor rules found.
/// </summary>
public sealed class LaborRuleResult
{
    /// <summary>
    /// Gets or sets the rule set applied, or <see langword="null"/> when none applies.
    /// </summary>
    public LaborRuleSet RuleSet { get; set; }

    /// <summary>
    /// Gets or sets the broken rules.
    /// </summary>
    public IList<LaborRuleViolation> Violations { get; set; } = [];

    /// <summary>
    /// Gets whether a broken rule refuses the change.
    /// </summary>
    public bool IsBlocked => Violations.Any(violation => violation.Enforcement == LaborRuleEnforcement.Block);

    /// <summary>
    /// Gets whether a broken rule needs an override reason.
    /// </summary>
    public bool NeedsOverride => Violations.Any(violation => violation.Enforcement == LaborRuleEnforcement.RequireOverride);
}
