using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Core.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// The default provider-agnostic taxation engine. It orchestrates external determination providers,
/// sourcing, jurisdiction resolution, rule resolution, exemptions, nexus, and calculation methods to
/// produce a deterministic and auditable tax breakdown. The engine contains no country-specific behavior.
/// </summary>
public sealed class TaxService : ITaxService
{
    private readonly IReadOnlyList<ITaxDeterminationProvider> _determinationProviders;
    private readonly ITaxableBaseCalculator _taxableBaseCalculator;
    private readonly ITaxSourcingStrategyProvider _sourcingProvider;
    private readonly ITaxJurisdictionResolver _jurisdictionResolver;
    private readonly ITaxRuleProvider _ruleProvider;
    private readonly ITaxExemptionResolver _exemptionResolver;
    private readonly IMerchantTaxRegistrationProvider _registrationProvider;
    private readonly ITaxCalculationMethodProvider _methodProvider;
    private readonly ITaxTableStore _tableStore;
    private readonly ITaxRoundingStrategy _roundingStrategy;
    private readonly TaxationOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxService"/> class.
    /// </summary>
    /// <param name="determinationProviders">The external determination providers.</param>
    /// <param name="taxableBaseCalculator">The taxable base calculator.</param>
    /// <param name="sourcingProvider">The sourcing strategy provider.</param>
    /// <param name="jurisdictionResolver">The jurisdiction resolver.</param>
    /// <param name="ruleProvider">The rule provider.</param>
    /// <param name="exemptionResolver">The exemption resolver.</param>
    /// <param name="registrationProvider">The merchant registration (nexus) provider.</param>
    /// <param name="methodProvider">The calculation method provider.</param>
    /// <param name="tableStore">The tax table store.</param>
    /// <param name="roundingStrategy">The rounding strategy.</param>
    /// <param name="options">The taxation options.</param>
    /// <param name="logger">The logger.</param>
    public TaxService(
        IEnumerable<ITaxDeterminationProvider> determinationProviders,
        ITaxableBaseCalculator taxableBaseCalculator,
        ITaxSourcingStrategyProvider sourcingProvider,
        ITaxJurisdictionResolver jurisdictionResolver,
        ITaxRuleProvider ruleProvider,
        ITaxExemptionResolver exemptionResolver,
        IMerchantTaxRegistrationProvider registrationProvider,
        ITaxCalculationMethodProvider methodProvider,
        ITaxTableStore tableStore,
        ITaxRoundingStrategy roundingStrategy,
        IOptions<TaxationOptions> options,
        ILogger<TaxService> logger)
    {
        _determinationProviders = determinationProviders.OrderBy(provider => provider.Order).ToArray();
        _taxableBaseCalculator = taxableBaseCalculator;
        _sourcingProvider = sourcingProvider;
        _jurisdictionResolver = jurisdictionResolver;
        _ruleProvider = ruleProvider;
        _exemptionResolver = exemptionResolver;
        _registrationProvider = registrationProvider;
        _methodProvider = methodProvider;
        _tableStore = tableStore;
        _roundingStrategy = roundingStrategy;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TaxCalculationResult> CalculateAsync(TaxCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var provider in _determinationProviders)
        {
            if (provider.CanHandle(context))
            {
                return await provider.DetermineAsync(context, cancellationToken);
            }
        }

        var result = new TaxCalculationResult
        {
            Currency = context.Currency,
        };

        var roundingLevel = context.RoundingLevel ?? _options.RoundingLevel;

        decimal sumNominal = 0m;
        decimal sumNet = 0m;

        foreach (var item in context.Items)
        {
            var itemState = await CalculateItemAsync(context, item, result, cancellationToken);

            sumNominal += itemState.NominalBase;
            sumNet += itemState.NetBase;
        }

        var totalTax = ApplyRounding(result.Lines, roundingLevel, context.Currency);
        var exclusiveTax = result.Lines.Where(line => !line.IncludedInPrice).Sum(line => line.TaxAmount);

        result.TaxableAmount = _roundingStrategy.Round(sumNet, context.Currency);
        result.TaxAmount = totalTax;
        result.TotalAmount = _roundingStrategy.Round(sumNominal, context.Currency) + exclusiveTax;

        return result;
    }

    private async ValueTask<ItemTaxState> CalculateItemAsync(
        TaxCalculationContext context,
        ITaxableItem item,
        TaxCalculationResult result,
        CancellationToken cancellationToken)
    {
        var nominalBase = _taxableBaseCalculator.GetTaxableBase(item, context);

        var address = ResolveAddress(context, item);
        var jurisdictions = await _jurisdictionResolver.ResolveAsync(address, context.TransactionDateUtc, cancellationToken);
        var jurisdictionLookup = BuildJurisdictionLookup(jurisdictions);

        var query = new TaxRuleQuery
        {
            JurisdictionIds = jurisdictions.Select(jurisdiction => jurisdiction.ItemId).ToArray(),
            CategoryCode = item.TaxCategoryCode,
            ClassificationCode = item.TaxClassificationCode,
            CustomerType = context.Customer?.CustomerType,
            TransactionDateUtc = context.TransactionDateUtc,
            IsShipping = item.Kind == TaxableItemKind.Shipping,
            TaxableAmount = nominalBase,
        };

        var candidateRules = await _ruleProvider.GetApplicableRulesAsync(query, cancellationToken);
        var applicableRules = await FilterRulesAsync(context, candidateRules, cancellationToken);

        var itemInclusive = item.PriceIncludesTax ?? (context.DefaultPriceType == TaxPriceType.Inclusive);
        var netBase = ComputeNetBase(nominalBase, applicableRules, itemInclusive);

        decimal priorItemTax = 0m;

        foreach (var rule in applicableRules)
        {
            var method = _methodProvider.GetMethod(rule.CalculationMethod);

            if (method is null)
            {
                _logger.LogWarning("No tax calculation method is registered for '{Method}'. The rule '{Rule}' was skipped.", rule.CalculationMethod, rule.ItemId);

                continue;
            }

            var effectiveIncluded = itemInclusive || rule.IncludedInPrice;
            var baseAmount = rule.IsCompound ? netBase + priorItemTax : netBase;
            var table = await GetTableAsync(rule.TaxTableId, cancellationToken);

            var computation = method.Compute(new TaxComputationRequest
            {
                TaxableBase = baseAmount,
                Quantity = item.Quantity,
                Weight = item.Weight,
                Volume = item.Volume,
                Rate = rule.Rate,
                FixedAmount = rule.FixedAmount,
                Table = table,
                PriceIncludesTax = false,
            });

            jurisdictionLookup.TryGetValue(rule.JurisdictionId ?? string.Empty, out var jurisdiction);

            result.Lines.Add(new TaxLine
            {
                ItemId = item.Id,
                TaxCode = rule.TaxCode,
                TaxName = rule.TaxName,
                TaxType = rule.TaxType,
                JurisdictionId = rule.JurisdictionId,
                JurisdictionName = jurisdiction?.Name,
                Rate = computation.EffectiveRate,
                TaxableAmount = baseAmount,
                TaxAmount = computation.TaxAmount,
                CalculationMethod = rule.CalculationMethod,
                IncludedInPrice = effectiveIncluded,
                IsCompound = rule.IsCompound,
                RuleId = rule.ItemId,
                RuleVersion = rule.Version,
                TableId = rule.TaxTableId,
                TableVersion = table?.Version ?? 0,
            });

            priorItemTax += computation.TaxAmount;
        }

        return new ItemTaxState(nominalBase, netBase);
    }

    private static decimal ComputeNetBase(decimal nominalBase, IReadOnlyList<TaxRule> rules, bool itemInclusive)
    {
        decimal sumInclusiveRates = 0m;
        var anyInclusive = false;

        foreach (var rule in rules)
        {
            var effectiveIncluded = itemInclusive || rule.IncludedInPrice;

            if (effectiveIncluded &&
                !rule.IsCompound &&
                string.Equals(rule.CalculationMethod, TaxCalculationMethodNames.Percentage, StringComparison.OrdinalIgnoreCase) &&
                rule.Rate.HasValue)
            {
                sumInclusiveRates += rule.Rate.Value;
                anyInclusive = true;
            }
        }

        return anyInclusive ? nominalBase / (1 + sumInclusiveRates) : nominalBase;
    }

    private async ValueTask<IReadOnlyList<TaxRule>> FilterRulesAsync(
        TaxCalculationContext context,
        IReadOnlyList<TaxRule> rules,
        CancellationToken cancellationToken)
    {
        var applicable = new List<TaxRule>();

        foreach (var rule in rules)
        {
            if (await _exemptionResolver.IsExemptAsync(context.Customer, rule, context.TransactionDateUtc, cancellationToken))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(rule.JurisdictionId) &&
                !await _registrationProvider.HasNexusAsync(rule.JurisdictionId, rule.TaxType, context.TransactionDateUtc, cancellationToken))
            {
                continue;
            }

            applicable.Add(rule);
        }

        return applicable;
    }

    private TaxAddress ResolveAddress(TaxCalculationContext context, ITaxableItem item)
    {
        var strategyName = item.Kind switch
        {
            TaxableItemKind.Digital => TaxSourcingNames.Destination,
            TaxableItemKind.Service => TaxSourcingNames.ServiceLocation,
            TaxableItemKind.Booking => TaxSourcingNames.ServiceLocation,
            TaxableItemKind.Event => TaxSourcingNames.EventLocation,
            _ => TaxSourcingNames.Destination,
        };

        var strategy = _sourcingProvider.GetStrategy(strategyName);
        var address = strategy?.Resolve(context, item);

        return address
            ?? context.Destination
            ?? item.Origin
            ?? context.Origin
            ?? context.Customer?.ResidenceAddress;
    }

    private async ValueTask<TaxTable> GetTableAsync(string tableId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(tableId))
        {
            return null;
        }

        return await _tableStore.FindByIdAsync(tableId, cancellationToken);
    }

    private static Dictionary<string, TaxJurisdiction> BuildJurisdictionLookup(IReadOnlyList<TaxJurisdiction> jurisdictions)
    {
        var lookup = new Dictionary<string, TaxJurisdiction>(StringComparer.Ordinal);

        foreach (var jurisdiction in jurisdictions)
        {
            lookup[jurisdiction.ItemId] = jurisdiction;
        }

        return lookup;
    }

    private decimal ApplyRounding(IList<TaxLine> lines, TaxRoundingLevel level, string currency)
    {
        if (lines.Count == 0)
        {
            return 0m;
        }

        if (level == TaxRoundingLevel.Line)
        {
            decimal total = 0m;

            foreach (var line in lines)
            {
                line.TaxAmount = _roundingStrategy.Round(line.TaxAmount, currency);
                total += line.TaxAmount;
            }

            return total;
        }

        var buckets = level switch
        {
            TaxRoundingLevel.Tax => lines.GroupBy(line => line.TaxType ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            TaxRoundingLevel.Jurisdiction => lines.GroupBy(line => line.JurisdictionId ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            _ => lines.GroupBy(_ => string.Empty),
        };

        decimal grandTotal = 0m;

        foreach (var bucket in buckets)
        {
            var bucketLines = bucket.ToArray();
            var roundedBucketTotal = _roundingStrategy.Round(bucketLines.Sum(line => line.TaxAmount), currency);

            decimal roundedLineSum = 0m;

            foreach (var line in bucketLines)
            {
                line.TaxAmount = _roundingStrategy.Round(line.TaxAmount, currency);
                roundedLineSum += line.TaxAmount;
            }

            var residual = roundedBucketTotal - roundedLineSum;

            if (residual != 0m)
            {
                bucketLines[bucketLines.Length - 1].TaxAmount += residual;
            }

            grandTotal += roundedBucketTotal;
        }

        return grandTotal;
    }

    private readonly struct ItemTaxState
    {
        public ItemTaxState(decimal nominalBase, decimal netBase)
        {
            NominalBase = nominalBase;
            NetBase = netBase;
        }

        public decimal NominalBase { get; }

        public decimal NetBase { get; }
    }
}
