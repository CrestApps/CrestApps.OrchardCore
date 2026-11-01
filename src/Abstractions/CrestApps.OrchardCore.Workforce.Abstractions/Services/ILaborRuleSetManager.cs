using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Manages labor rule sets.
/// </summary>
public interface ILaborRuleSetManager : ICatalogManager<LaborRuleSet>
{
    /// <summary>
    /// Lists the rules in the catalog, by name.
    /// </summary>
    /// <returns>The rules.</returns>
    IReadOnlyList<ILaborRule> ListRules();

    /// <summary>
    /// Creates a rule for a set with the rule's default parameters.
    /// </summary>
    /// <param name="ruleKey">The rule's key.</param>
    /// <returns>The rule, or <see langword="null"/> when the key isn't in the catalog.</returns>
    LaborRuleInstance NewRule(string ruleKey);
}
