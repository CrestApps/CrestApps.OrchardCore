using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Default <see cref="ITaxRuleProvider"/> backed by the <see cref="INamedCatalog{TaxRule}"/>. Rules are filtered
/// by jurisdiction, classification, customer type, effective dates, thresholds, and shipping, then
/// ordered deterministically by priority, compound flag, and identifier.
/// </summary>
public sealed class CatalogTaxRuleProvider : ITaxRuleProvider
{
    private readonly INamedCatalog<TaxRule> _store;
    private readonly INamedCatalog<TaxCategory> _categories;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogTaxRuleProvider"/> class.
    /// </summary>
    /// <param name="store">The tax rule store.</param>
    /// <param name="categories">The tax categories, whose parents a rule for a broader category also covers.</param>
    public CatalogTaxRuleProvider(INamedCatalog<TaxRule> store, INamedCatalog<TaxCategory> categories)
    {
        _store = store;
        _categories = categories;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<TaxRule>> GetApplicableRulesAsync(TaxRuleQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rules = await _store.GetAllAsync(cancellationToken);

        // A rule for a category also covers the categories under it: a rule for Electronics taxes an item
        // classified as Television when Television's parent is Electronics.
        var categoryLineage = rules.Any(rule => !string.IsNullOrEmpty(rule.CategoryCode))
            ? await GetCategoryLineageAsync(query.CategoryCode, cancellationToken)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var applicable = rules.Where(rule => IsApplicable(rule, query, categoryLineage));

        return TaxRuleOrdering.Order(applicable).ToArray();
    }

    private static bool IsApplicable(TaxRule rule, TaxRuleQuery query, HashSet<string> categoryLineage)
    {
        if (!rule.Enabled)
        {
            return false;
        }

        // A rule for a jurisdiction applies only where that jurisdiction was resolved for the item. When none was
        // resolved (no address, or one outside every jurisdiction), such a rule doesn't apply: taxing a buyer for a
        // place they may not be in is worse than leaving it to a rule with no jurisdiction. Rules with no
        // jurisdiction apply everywhere.
        if (!string.IsNullOrEmpty(rule.JurisdictionId) &&
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
            !MatchesCategory(rule.CategoryCode, query, categoryLineage))
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

        // An order-wide threshold, such as an import duty's, is compared with what the order's items come to.
        var amount = rule.ThresholdBasis == TaxThresholdBasis.Order ? query.OrderAmount ?? query.TaxableAmount : query.TaxableAmount;

        if (rule.MinimumAmount.HasValue && amount < rule.MinimumAmount.Value)
        {
            return false;
        }

        if (rule.MaximumAmount.HasValue && amount >= rule.MaximumAmount.Value)
        {
            return false;
        }

        return true;
    }

    private static bool MatchesCategory(string ruleCategory, TaxRuleQuery query, HashSet<string> categoryLineage)
    {
        return string.Equals(ruleCategory, query.CategoryCode, StringComparison.OrdinalIgnoreCase) ||
            categoryLineage.Contains(ruleCategory) ||
            string.Equals(ruleCategory, query.ClassificationCode, StringComparison.OrdinalIgnoreCase);
    }

    // The item's category and every category above it, by code. A loop in the parents ends the walk rather than
    // spinning forever.
    private async Task<HashSet<string>> GetCategoryLineageAsync(string categoryCode, CancellationToken cancellationToken)
    {
        var lineage = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(categoryCode))
        {
            return lineage;
        }

        var parents = (await _categories.GetAllAsync(cancellationToken))
            .Where(category => !string.IsNullOrEmpty(category.Code))
            .GroupBy(category => category.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().ParentCode, StringComparer.OrdinalIgnoreCase);

        var code = categoryCode;

        while (!string.IsNullOrEmpty(code) && lineage.Add(code))
        {
            code = parents.TryGetValue(code, out var parent) ? parent : null;
        }

        return lineage;
    }
}
