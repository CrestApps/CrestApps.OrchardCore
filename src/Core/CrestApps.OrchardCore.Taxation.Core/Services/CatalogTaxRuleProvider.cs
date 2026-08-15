using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Default <see cref="ITaxRuleProvider"/> backed by the <see cref="ITaxRuleStore"/>. Rules are filtered
/// by jurisdiction, classification, customer type, effective dates, thresholds, and shipping, then
/// ordered deterministically by priority, compound flag, and identifier.
/// </summary>
public sealed class CatalogTaxRuleProvider : ITaxRuleProvider
{
    private readonly ITaxRuleStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogTaxRuleProvider"/> class.
    /// </summary>
    /// <param name="store">The tax rule store.</param>
    public CatalogTaxRuleProvider(ITaxRuleStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<TaxRule>> GetApplicableRulesAsync(TaxRuleQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rules = await _store.GetAllAsync(cancellationToken);

        var applicable = rules.Where(rule => IsApplicable(rule, query));

        return TaxRuleOrdering.Order(applicable).ToArray();
    }

    private static bool IsApplicable(TaxRule rule, TaxRuleQuery query)
    {
        if (!rule.Enabled)
        {
            return false;
        }

        if (query.JurisdictionIds.Count > 0 &&
            !string.IsNullOrEmpty(rule.JurisdictionId) &&
            !query.JurisdictionIds.Contains(rule.JurisdictionId))
        {
            return false;
        }

        if (rule.EffectiveFromUtc.HasValue && query.TransactionDateUtc < rule.EffectiveFromUtc.Value)
        {
            return false;
        }

        if (rule.EffectiveToUtc.HasValue && query.TransactionDateUtc >= rule.EffectiveToUtc.Value)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(rule.CategoryCode) &&
            !MatchesCategory(rule.CategoryCode, query))
        {
            return false;
        }

        if (rule.CustomerType.HasValue && query.CustomerType.HasValue && rule.CustomerType.Value != query.CustomerType.Value)
        {
            return false;
        }

        if (query.IsShipping && !rule.AppliesToShipping)
        {
            return false;
        }

        if (rule.MinimumAmount.HasValue && query.TaxableAmount < rule.MinimumAmount.Value)
        {
            return false;
        }

        if (rule.MaximumAmount.HasValue && query.TaxableAmount >= rule.MaximumAmount.Value)
        {
            return false;
        }

        return true;
    }

    private static bool MatchesCategory(string ruleCategory, TaxRuleQuery query)
    {
        return string.Equals(ruleCategory, query.CategoryCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ruleCategory, query.ClassificationCode, StringComparison.OrdinalIgnoreCase);
    }
}
