using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Taxation.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.Localization;
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
    private readonly INamedCatalog<TaxTable> _tableStore;
    private readonly ITaxRoundingStrategy _roundingStrategy;
    private readonly TaxationOptions _options;
    private readonly IStringLocalizer S;
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
    /// <param name="stringLocalizer">The string localizer.</param>
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
        INamedCatalog<TaxTable> tableStore,
        ITaxRoundingStrategy roundingStrategy,
        IOptions<TaxationOptions> options,
        IStringLocalizer<TaxService> stringLocalizer,
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
        S = stringLocalizer;
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
        var classification = await ClassifyRulesAsync(context, candidateRules, cancellationToken);

        var applicableRules = classification.Applicable;

        var itemInclusive = item.PriceIncludesTax ?? (context.DefaultPriceType == TaxPriceType.Inclusive);
        var netBase = ComputeNetBase(nominalBase, applicableRules, item, itemInclusive);

        decimal priorItemTax = 0m;

        foreach (var rule in applicableRules)
        {
            var method = _methodProvider.GetMethod(rule.Source);

            if (method is null)
            {
                _logger.LogWarning("No tax calculation method is registered for '{Method}'. The rule '{Rule}' was skipped.", rule.Source, rule.ItemId);

                continue;
            }

            var effectiveIncluded = itemInclusive || rule.IncludedInPrice;
            var baseAmount = rule.IsCompound ? netBase + priorItemTax : netBase;
            var table = await GetTableAsync(rule.TaxTableId, context.TransactionDateUtc, cancellationToken);

            if (method.Inputs.HasFlag(TaxCalculationMethodInputs.TaxTable) && table is null)
            {
                _logger.LogWarning(
                    "The rule '{Rule}' uses a table-driven method but its tax table is missing or is not effective on {Date:o}. The rule was skipped so an inactive table cannot silently produce zero tax.",
                    rule.ItemId,
                    context.TransactionDateUtc);

                continue;
            }

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
                TaxName = string.IsNullOrEmpty(rule.TaxName) ? rule.Name : rule.TaxName,
                TaxType = rule.TaxType,
                JurisdictionId = rule.JurisdictionId,
                JurisdictionName = jurisdiction?.Name,
                Rate = computation.EffectiveRate,
                TaxableAmount = baseAmount,
                TaxAmount = computation.TaxAmount,
                CalculationMethod = rule.Source,
                IncludedInPrice = effectiveIncluded,
                IsCompound = rule.IsCompound,
                RuleId = rule.ItemId,
                RuleVersion = rule.Version,
                TableId = rule.TaxTableId,
                TableVersion = table?.Version ?? 0,
            });

            priorItemTax += computation.TaxAmount;
        }

        foreach (var zeroRated in classification.ZeroRated)
        {
            var rule = zeroRated.Rule;

            jurisdictionLookup.TryGetValue(rule.JurisdictionId ?? string.Empty, out var jurisdiction);

            result.Lines.Add(new TaxLine
            {
                ItemId = item.Id,
                TaxCode = rule.TaxCode,
                TaxName = string.IsNullOrEmpty(rule.TaxName) ? rule.Name : rule.TaxName,
                TaxType = rule.TaxType,
                JurisdictionId = rule.JurisdictionId,
                JurisdictionName = jurisdiction?.Name,
                Rate = 0m,
                TaxableAmount = netBase,
                TaxAmount = 0m,
                CalculationMethod = rule.Source,
                IncludedInPrice = itemInclusive || rule.IncludedInPrice,
                IsCompound = rule.IsCompound,
                RuleId = rule.ItemId,
                RuleVersion = rule.Version,
                Treatment = zeroRated.Treatment,
                TreatmentReason = zeroRated.Reason,
            });
        }

        return new ItemTaxState(nominalBase, netBase);
    }

    private decimal ComputeNetBase(decimal nominalBase, IReadOnlyList<TaxRule> rules, ITaxableItem item, bool itemInclusive)
    {
        decimal sumInclusiveRates = 0m;
        decimal sumInclusiveFixed = 0m;
        var anyInclusive = false;

        foreach (var rule in rules)
        {
            var effectiveIncluded = itemInclusive || rule.IncludedInPrice;

            if (!effectiveIncluded || rule.IsCompound)
            {
                continue;
            }

            if (string.Equals(rule.Source, TaxCalculationMethodNames.Percentage, StringComparison.OrdinalIgnoreCase) &&
                rule.Rate.HasValue)
            {
                sumInclusiveRates += rule.Rate.Value;
                anyInclusive = true;
            }
            else if (IsFixedAmountFamily(rule.Source))
            {
                var method = _methodProvider.GetMethod(rule.Source);

                if (method is null)
                {
                    continue;
                }

                var computation = method.Compute(new TaxComputationRequest
                {
                    TaxableBase = 0m,
                    Quantity = item.Quantity,
                    Weight = item.Weight,
                    Volume = item.Volume,
                    Rate = rule.Rate,
                    FixedAmount = rule.FixedAmount,
                    PriceIncludesTax = false,
                });

                sumInclusiveFixed += computation.TaxAmount;
                anyInclusive = true;
            }
        }

        if (!anyInclusive)
        {
            return nominalBase;
        }

        var net = (nominalBase - sumInclusiveFixed) / (1 + sumInclusiveRates);

        return net < 0m ? 0m : net;
    }

    private static bool IsFixedAmountFamily(string source)
        => string.Equals(source, TaxCalculationMethodNames.FixedAmount, StringComparison.OrdinalIgnoreCase)
        || string.Equals(source, TaxCalculationMethodNames.PerUnit, StringComparison.OrdinalIgnoreCase)
        || string.Equals(source, TaxCalculationMethodNames.PerWeight, StringComparison.OrdinalIgnoreCase)
        || string.Equals(source, TaxCalculationMethodNames.PerVolume, StringComparison.OrdinalIgnoreCase);

    private async ValueTask<RuleClassification> ClassifyRulesAsync(
        TaxCalculationContext context,
        IReadOnlyList<TaxRule> rules,
        CancellationToken cancellationToken)
    {
        var applicable = new List<TaxRule>();
        var zeroRated = new List<ZeroRatedRule>();

        foreach (var rule in rules)
        {
            if (!string.IsNullOrEmpty(rule.JurisdictionId) &&
                !await _registrationProvider.HasNexusAsync(rule.JurisdictionId, rule.TaxType, context.TransactionDateUtc, cancellationToken))
            {
                continue;
            }

            if (rule.ReverseCharge && context.Customer?.CustomerType == CustomerTaxType.B2B)
            {
                zeroRated.Add(new ZeroRatedRule(rule, TaxTreatment.ReverseCharge, S["Reverse charge — the customer accounts for the tax."]));

                continue;
            }

            if (await _exemptionResolver.IsExemptAsync(context.Customer, rule, context.TransactionDateUtc, cancellationToken))
            {
                zeroRated.Add(new ZeroRatedRule(rule, TaxTreatment.Exempt, S["The customer is exempt from this tax."]));

                continue;
            }

            applicable.Add(rule);
        }

        return new RuleClassification(applicable, zeroRated);
    }

    private Address ResolveAddress(TaxCalculationContext context, ITaxableItem item)
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

    private async ValueTask<TaxTable> GetTableAsync(string tableId, DateTime transactionDateUtc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(tableId))
        {
            return null;
        }

        var table = await _tableStore.FindByIdAsync(tableId, cancellationToken);

        if (table is null || !IsEffectiveOn(table, transactionDateUtc))
        {
            return null;
        }

        return table;
    }

    // A tax table only applies to a transaction that falls within its effective window, so an expired or
    // not-yet-effective table is never used to calculate tax for a dated transaction. The end bound is
    // exclusive, matching the effective-window semantics used for tax rules.
    internal static bool IsEffectiveOn(TaxTable table, DateTime transactionDateUtc)
        => (!table.EffectiveFromUtc.HasValue || transactionDateUtc >= table.EffectiveFromUtc.Value) &&
            (!table.EffectiveToUtc.HasValue || transactionDateUtc < table.EffectiveToUtc.Value);

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

    private readonly struct RuleClassification
    {
        public RuleClassification(IReadOnlyList<TaxRule> applicable, IReadOnlyList<ZeroRatedRule> zeroRated)
        {
            Applicable = applicable;
            ZeroRated = zeroRated;
        }

        public IReadOnlyList<TaxRule> Applicable { get; }

        public IReadOnlyList<ZeroRatedRule> ZeroRated { get; }
    }

    private readonly struct ZeroRatedRule
    {
        public ZeroRatedRule(TaxRule rule, TaxTreatment treatment, string reason)
        {
            Rule = rule;
            Treatment = treatment;
            Reason = reason;
        }

        public TaxRule Rule { get; }

        public TaxTreatment Treatment { get; }

        public string Reason { get; }
    }
}
