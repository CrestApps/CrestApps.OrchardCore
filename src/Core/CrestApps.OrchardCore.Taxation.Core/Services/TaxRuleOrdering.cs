using System.Collections.Generic;
using System.Linq;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Provides deterministic ordering of tax rules so that a given set of rules always resolves to the
/// same sequence. Non-compound rules are evaluated before compound rules.
/// </summary>
public static class TaxRuleOrdering
{
    /// <summary>
    /// Orders the supplied rules by priority, then by compound flag, then by identifier.
    /// </summary>
    /// <param name="rules">The rules to order.</param>
    /// <returns>The ordered rules.</returns>
    public static IEnumerable<TaxRule> Order(IEnumerable<TaxRule> rules)
    {
        return rules
            .OrderBy(rule => rule.IsCompound)
            .ThenBy(rule => rule.Priority)
            .ThenBy(rule => rule.ItemId, System.StringComparer.Ordinal);
    }
}
