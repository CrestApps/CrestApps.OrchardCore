using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A labor rule in the catalog. Register one with <see cref="LaborRuleServiceCollectionExtensions.AddLaborRule{TRule}"/>;
/// admins add it to rule sets with their own parameters and enforcement.
/// </summary>
public interface ILaborRule
{
    /// <summary>
    /// Gets the rule's key, stored in rule sets.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets the rule's name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets what the rule checks.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the rule's parameters.
    /// </summary>
    IReadOnlyList<LaborRuleParameter> Parameters { get; }

    /// <summary>
    /// Gets whether the rule only records something, such as predictability pay, and has no enforcement.
    /// </summary>
    bool RecordsOnly => false;

    /// <summary>
    /// Checks the shifts in the context and reports what breaks the rule.
    /// </summary>
    /// <param name="context">The shifts, the changes and the violations so far.</param>
    /// <param name="instance">The rule's parameters and enforcement in the set.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default);
}
