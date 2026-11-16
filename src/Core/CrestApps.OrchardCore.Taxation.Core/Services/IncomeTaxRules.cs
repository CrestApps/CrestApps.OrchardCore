using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Localization;
using OrchardCore.Modules;
using YesSql.Indexes;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Indexes income tax rules.
/// </summary>
public sealed class IncomeTaxRuleIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the tax's code.</summary>
    public string TaxCode { get; set; }

    /// <summary>Gets or sets the jurisdiction.</summary>
    public string Jurisdiction { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; }
}

/// <summary>
/// Maps income tax rules to <see cref="IncomeTaxRuleIndex"/> rows.
/// </summary>
public sealed class IncomeTaxRuleIndexProvider : IndexProvider<IncomeTaxRule>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IncomeTaxRuleIndexProvider"/> class.
    /// </summary>
    public IncomeTaxRuleIndexProvider()
    {
        CollectionName = TaxationConstants.IncomeTaxCollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<IncomeTaxRule> context)
    {
        context
            .For<IncomeTaxRuleIndex>()
            .Map(rule => new IncomeTaxRuleIndex
            {
                ItemId = rule.ItemId,
                TaxCode = rule.TaxCode,
                Jurisdiction = rule.Jurisdiction,
                Name = rule.Name,
            });
    }
}

/// <summary>
/// Creates the table of income tax rules.
/// </summary>
public static class IncomeTaxRuleIndexSchema
{
    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<IncomeTaxRuleIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("TaxCode", column => column.Nullable().WithLength(50))
            .Column<string>("Jurisdiction", column => column.Nullable().WithLength(50))
            .Column<string>("Name", column => column.Nullable().WithLength(255)),
            collection: TaxationConstants.IncomeTaxCollectionName);

        await schemaBuilder.AlterIndexTableAsync<IncomeTaxRuleIndex>(table => table
            .CreateIndex("IDX_IncomeTaxRuleIndex_ItemId", "ItemId", "DocumentId"),
            collection: TaxationConstants.IncomeTaxCollectionName);
    }
}

/// <summary>
/// Stores income tax rules in the Income Tax collection.
/// </summary>
public sealed class IncomeTaxRuleStore : DocumentCatalog<IncomeTaxRule, IncomeTaxRuleIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IncomeTaxRuleStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public IncomeTaxRuleStore(ISession session)
        : base(session)
    {
        CollectionName = TaxationConstants.IncomeTaxCollectionName;
    }
}

/// <summary>
/// The default <see cref="IIncomeTaxRuleManager"/>.
/// </summary>
public sealed class IncomeTaxRuleManager : CatalogManager<IncomeTaxRule>, IIncomeTaxRuleManager
{
    private readonly IncomeTaxRuleStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="IncomeTaxRuleManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public IncomeTaxRuleManager(IncomeTaxRuleStore store, IEnumerable<ICatalogEntryHandler<IncomeTaxRule>> handlers, ILogger<CatalogManager<IncomeTaxRule>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IncomeTaxRule>> ListAsync(CancellationToken cancellationToken = default)
        => [.. await _store.GetAllAsync(cancellationToken)];
}

/// <summary>
/// Stamps income tax rules and checks them: a code, a jurisdiction, a known method with what it needs, rates from 0 to
/// 100, amounts of zero or more, a share of another tax with its multiplier, and no two versions of one tax in effect
/// on the same day.
/// </summary>
public sealed class IncomeTaxRuleHandler : CatalogEntryHandlerBase<IncomeTaxRule>
{
    private readonly IClock _clock;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="IncomeTaxRuleHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="services">The services, for the calculation methods and the other rules.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public IncomeTaxRuleHandler(IClock clock, IServiceProvider services, IStringLocalizer<IncomeTaxRuleHandler> stringLocalizer)
    {
        _clock = clock;
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task InitializingAsync(InitializingContext<IncomeTaxRule> context, CancellationToken cancellationToken = default)
    {
        // A recipe or a deployment plan hands the values in; a page sets them itself and passes none.
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Normalize(context.Model);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task InitializedAsync(InitializedContext<IncomeTaxRule> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<IncomeTaxRule> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Normalize(context.Model);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    // Trims and upper-cases what a page, a recipe or a deployment plan typed.
    private static void Normalize(IncomeTaxRule rule)
    {
        rule.Name = rule.Name?.Trim();
        rule.TaxCode = rule.TaxCode?.Trim().ToUpperInvariant();
        rule.Jurisdiction = rule.Jurisdiction?.Trim().ToUpperInvariant();
        rule.CollectedBy = string.IsNullOrWhiteSpace(rule.CollectedBy) ? null : rule.CollectedBy.Trim().ToUpperInvariant();
        rule.ShareOfTaxCode = string.IsNullOrWhiteSpace(rule.ShareOfTaxCode) ? null : rule.ShareOfTaxCode.Trim().ToUpperInvariant();
        rule.ExceptJurisdictions = [.. (rule.ExceptJurisdictions ?? [])
            .Where(jurisdiction => !string.IsNullOrWhiteSpace(jurisdiction))
            .Select(jurisdiction => jurisdiction.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)];
        rule.IncomeKinds = [.. (rule.IncomeKinds ?? []).Distinct()];
    }

    /// <inheritdoc />
    public override async Task ValidatingAsync(ValidatingContext<IncomeTaxRule> context, CancellationToken cancellationToken = default)
    {
        var rule = context.Model;

        Normalize(rule);

        if (string.IsNullOrEmpty(rule.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(IncomeTaxRule.Name)]));
        }

        if (string.IsNullOrEmpty(rule.TaxCode))
        {
            context.Result.Fail(new ValidationResult(S["The tax code is required."], [nameof(IncomeTaxRule.TaxCode)]));
        }

        if (string.IsNullOrEmpty(rule.Jurisdiction))
        {
            context.Result.Fail(new ValidationResult(S["The jurisdiction is required."], [nameof(IncomeTaxRule.Jurisdiction)]));
        }

        if (string.IsNullOrEmpty(rule.Category))
        {
            context.Result.Fail(new ValidationResult(S["Choose the tax category whose income is taxed."], [nameof(IncomeTaxRule.Category)]));
        }

        if (rule.ShareOfTaxCode is not null)
        {
            // A share takes another tax's amount; it needs no method of its own.
            if (rule.ShareMultiplier is not > 0)
            {
                context.Result.Fail(new ValidationResult(S["A share of another tax needs a multiplier above zero."], [nameof(IncomeTaxRule.ShareMultiplier)]));
            }

            if (string.Equals(rule.ShareOfTaxCode, rule.TaxCode, StringComparison.Ordinal))
            {
                context.Result.Fail(new ValidationResult(S["A tax can't be a share of itself."], [nameof(IncomeTaxRule.ShareOfTaxCode)]));
            }
        }
        else
        {
            ValidateMethod(context, rule);
        }

        if (rule.Rate is < 0 or > 100 || rule.SupplementalRate is < 0 or > 100 || rule.SupplementalExcessRate is < 0 or > 100)
        {
            context.Result.Fail(new ValidationResult(S["A rate is a percent from 0 to 100."], [nameof(IncomeTaxRule.Rate)]));
        }

        if (rule.FixedAmount is < 0 || rule.WageBase is < 0 || rule.AllowanceAmount is < 0 || rule.WageThreshold is < 0 || rule.SupplementalExcessThreshold is < 0 || rule.YearlyExemption is < 0 || rule.YearlyMaximum is < 0)
        {
            context.Result.Fail(new ValidationResult(S["An amount is zero or more."], [nameof(IncomeTaxRule.WageBase)]));
        }

        if (rule.EffectiveFrom == default)
        {
            context.Result.Fail(new ValidationResult(S["Give the first day the rule applies."], [nameof(IncomeTaxRule.EffectiveFrom)]));
        }

        if (rule.EffectiveTo is { } to && to < rule.EffectiveFrom)
        {
            context.Result.Fail(new ValidationResult(S["The last day is before the first day."], [nameof(IncomeTaxRule.EffectiveTo)]));
        }

        if (!string.IsNullOrEmpty(rule.TaxCode) && _services.GetService<IncomeTaxRuleStore>() is { } store)
        {
            var overlapping = (await store.GetAllAsync(cancellationToken)).FirstOrDefault(other => other.ItemId != rule.ItemId
                && other.TaxCode == rule.TaxCode
                && other.Enabled && rule.Enabled
                && other.EffectiveFrom <= (rule.EffectiveTo ?? DateOnly.MaxValue)
                && rule.EffectiveFrom <= (other.EffectiveTo ?? DateOnly.MaxValue));

            if (overlapping is not null)
            {
                context.Result.Fail(new ValidationResult(S["Another version of {0} is in effect on some of the same days ({1}). Give it a last day first.", rule.TaxCode, overlapping.Name], [nameof(IncomeTaxRule.EffectiveFrom)]));
            }
        }
    }

    private void ValidateMethod(ValidatingContext<IncomeTaxRule> context, IncomeTaxRule rule)
    {
        var withholding = _services.GetServices<IIncomeTaxWithholdingMethod>().FirstOrDefault(candidate => string.Equals(candidate.Name, rule.Method, StringComparison.OrdinalIgnoreCase));
        var method = withholding is null ? _services.GetService<ITaxCalculationMethodProvider>()?.GetMethod(rule.Method) : null;

        if (withholding is not null)
        {
            foreach (var table in withholding.Tables.Where(table => table.Required && string.IsNullOrEmpty(rule.Tables?.GetValueOrDefault(table.Key))))
            {
                context.Result.Fail(new ValidationResult(S["The method needs the tax table {0}.", table.Label.Value], [nameof(IncomeTaxRule.Tables)]));
            }

            return;
        }

        if (method is null)
        {
            context.Result.Fail(new ValidationResult(S["Choose a calculation method."], [nameof(IncomeTaxRule.Method)]));

            return;
        }

        if (method.Inputs.HasFlag(TaxCalculationMethodInputs.Rate) && rule.Rate is null)
        {
            context.Result.Fail(new ValidationResult(S["The method needs a rate."], [nameof(IncomeTaxRule.Rate)]));
        }

        if (method.Inputs.HasFlag(TaxCalculationMethodInputs.FixedAmount) && rule.FixedAmount is null)
        {
            context.Result.Fail(new ValidationResult(S["The method needs a fixed amount."], [nameof(IncomeTaxRule.FixedAmount)]));
        }

        if (method.Inputs.HasFlag(TaxCalculationMethodInputs.TaxTable) && string.IsNullOrEmpty(rule.TaxTableId))
        {
            context.Result.Fail(new ValidationResult(S["The method needs a tax table."], [nameof(IncomeTaxRule.TaxTableId)]));
        }
    }
}

/// <summary>
/// The built-in income tax provider: the income tax rules in effect on the pay date, worked out with Taxation's
/// calculation methods over its tax tables, or with a country pack's withholding method. A rule applies when the payer
/// is registered in the jurisdiction that collects it, it is for the kind of income, and, for a jurisdiction below a
/// country (with a dash, such as US-CA or CA-ON), the person is in it (home or work); a person with neither is taxed by
/// every registered jurisdiction. A rule never applies to a person in one of its excepted jurisdictions. Contributions
/// and flat taxes are worked out first, then withholding methods that give credits for them, then the shares of other
/// taxes (such as an employer's share), and the lines come back in jurisdiction and tax code order.
/// </summary>
public sealed class IncomeTaxRulesProvider : IIncomeTaxProvider
{
    /// <summary>
    /// The provider's stored name.
    /// </summary>
    public const string ProviderName = "TaxTables";

    private static readonly LocalizationSource _displayName = LocalizationSource.Create<IncomeTaxRulesProvider>("Income tax rules");

    private readonly IIncomeTaxRuleManager _rules;
    private readonly ITaxCalculationMethodProvider _methods;
    private readonly INamedCatalog<TaxTable> _tables;
    private readonly IEnumerable<IIncomeTaxWithholdingMethod> _withholdingMethods;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="IncomeTaxRulesProvider"/> class.
    /// </summary>
    /// <param name="rules">The rules.</param>
    /// <param name="methods">The calculation methods.</param>
    /// <param name="tables">The tax tables.</param>
    /// <param name="withholdingMethods">The withholding methods country packs add.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public IncomeTaxRulesProvider(IIncomeTaxRuleManager rules, ITaxCalculationMethodProvider methods, INamedCatalog<TaxTable> tables, IEnumerable<IIncomeTaxWithholdingMethod> withholdingMethods, IStringLocalizer<IncomeTaxRulesProvider> stringLocalizer)
    {
        _rules = rules;
        _methods = methods;
        _tables = tables;
        _withholdingMethods = withholdingMethods;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public LocalizationSource DisplayName => _displayName;

    /// <inheritdoc />
    public async Task<IncomeTaxResult> CalculateAsync(IncomeTaxRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new IncomeTaxResult();

        // A contractor has only contractor withholding, and only when it applies.
        if (request.IncomeKind == IncomeKind.Contractor && !request.ContractorWithholding)
        {
            return result;
        }

        var registered = (request.RegisteredJurisdictions ?? [])
            .Where(jurisdiction => !string.IsNullOrEmpty(jurisdiction))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var personal = (request.HomeJurisdictions ?? []).Concat(request.WorkJurisdictions ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rules = (await _rules.ListAsync(cancellationToken))
            .Where(rule => rule.AppliesOn(request.PayDate)
                && registered.Contains(rule.CollectedBy ?? rule.Jurisdiction)
                && rule.AppliesTo(request.IncomeKind)
                && (!rule.Jurisdiction.Contains('-', StringComparison.Ordinal) || personal.Count == 0 || personal.Contains(rule.Jurisdiction))
                && !(rule.ExceptJurisdictions ?? []).Any(personal.Contains))
            .OrderBy(rule => rule.Jurisdiction, StringComparer.Ordinal)
            .ThenBy(rule => rule.TaxCode, StringComparer.Ordinal)
            .ToList();
        var lines = new List<(int Order, IncomeTaxLine Line)>();

        // Contributions and flat taxes, then the methods that give credits for them, then shares of other taxes.
        foreach (var phase in new[] { 0, 1, 2 })
        {
            for (var index = 0; index < rules.Count; index++)
            {
                var rule = rules[index];
                var withholding = WithholdingMethod(rule);
                var rulePhase = rule.ShareOfTaxCode is not null ? 2 : withholding?.UsesOtherTaxes == true ? 1 : 0;

                if (rulePhase != phase)
                {
                    continue;
                }

                var line = rule.ShareOfTaxCode is not null
                    ? Share(rule, request, lines.Select(pair => pair.Line))
                    : await CalculateRuleAsync(rule, withholding, request, [.. lines.Select(pair => pair.Line)], result, cancellationToken);

                if (line is not null)
                {
                    lines.Add((index, line));
                }
            }
        }

        foreach (var (_, line) in lines.OrderBy(pair => pair.Order))
        {
            result.Lines.Add(line);
        }

        return result;
    }

    private IIncomeTaxWithholdingMethod WithholdingMethod(IncomeTaxRule rule)
        => _withholdingMethods.FirstOrDefault(method => string.Equals(method.Name, rule.Method, StringComparison.OrdinalIgnoreCase));

    // A share of another tax of the same payment, such as an employer's share of a contribution, within its maximum.
    private static IncomeTaxLine Share(IncomeTaxRule rule, IncomeTaxRequest request, IEnumerable<IncomeTaxLine> lines)
    {
        var of = lines.Where(line => string.Equals(line.TaxCode, rule.ShareOfTaxCode, StringComparison.OrdinalIgnoreCase)).ToList();

        if (of.Count == 0)
        {
            return null;
        }

        var tax = Math.Round(of.Sum(line => line.Amount), 2, MidpointRounding.AwayFromZero) * (rule.ShareMultiplier ?? 1m);

        if (rule.YearlyMaximum is { } maximum)
        {
            tax = Math.Min(tax, Math.Max(0, maximum - request.ToDate(rule.TaxCode).YearTax));
        }

        return Line(rule, of.Sum(line => line.TaxableIncome), tax, of.Sum(line => line.FlatIncome));
    }

    private async Task<IncomeTaxLine> CalculateRuleAsync(IncomeTaxRule rule, IIncomeTaxWithholdingMethod withholding, IncomeTaxRequest request, IReadOnlyList<IncomeTaxLine> others, IncomeTaxResult result, CancellationToken cancellationToken)
    {
        List<string> tableIds = withholding is not null
            ? [.. withholding.Tables.Where(table => table.Required || !string.IsNullOrEmpty(rule.Tables?.GetValueOrDefault(table.Key))).Select(table => rule.Tables?.GetValueOrDefault(table.Key))]
            : _methods.GetMethod(rule.Method) is { } method && method.Inputs.HasFlag(TaxCalculationMethodInputs.TaxTable) ? [rule.TaxTableId] : [];
        var day = request.PayDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        foreach (var tableId in tableIds)
        {
            var table = string.IsNullOrEmpty(tableId) ? null : await _tables.FindByIdAsync(tableId, cancellationToken);

            if (table is null || (table.EffectiveFromUtc is { } from && from.Date > day) || (table.EffectiveToUtc is { } to && to.Date < day))
            {
                result.Warnings.Add(S["{0} was not worked out: its tax table is missing or not in effect on the pay date.", rule.Name]);

                return null;
            }
        }

        return await CalculateAsync(rule, withholding, request, others, cancellationToken);
    }

    private async Task<IncomeTaxLine> CalculateAsync(IncomeTaxRule rule, IIncomeTaxWithholdingMethod withholding, IncomeTaxRequest request, IReadOnlyList<IncomeTaxLine> others, CancellationToken cancellationToken)
    {
        var election = rule.UsesElections
            ? request.Elections.FirstOrDefault(candidate => string.Equals(candidate.Jurisdiction, rule.Jurisdiction, StringComparison.OrdinalIgnoreCase))
            : null;
        var regular = request.Income.TryGetValue(rule.Category, out var income) ? income : 0;
        var supplemental = request.SupplementalIncome.TryGetValue(rule.Category, out var supplementalIncome) ? supplementalIncome : 0;
        var periods = Math.Max(1, request.PeriodsPerYear);
        var toDate = request.ToDate(rule.TaxCode);

        // The part of the year's supplemental income above a threshold is withheld at its own rate, whatever the
        // method and the election (such as a mandatory rate on supplemental wages over a million).
        var excess = 0m;

        if (rule.SupplementalExcessRate is not null && rule.SupplementalExcessThreshold is { } excessThreshold && supplemental > 0)
        {
            var before = request.YearSupplementalIncome.TryGetValue(rule.Category, out var yearSupplemental) ? yearSupplemental : 0;

            excess = Math.Min(supplemental, Math.Max(0, before + supplemental - excessThreshold) - Math.Max(0, before - excessThreshold));
            supplemental -= excess;
        }

        var excessTax = excess * (rule.SupplementalExcessRate ?? 0) / 100m;

        if (election?.Value("Exempt") == "true")
        {
            return excess > 0 ? Line(rule, excess, excessTax, excess) : null;
        }

        var ownSupplemental = withholding?.HandlesSupplementalIncome == true;
        var flat = !ownSupplemental && request.SupplementalMethod == SupplementalWithholdingMethod.Flat && rule.SupplementalRate is not null;

        if (!flat && !ownSupplemental)
        {
            regular += supplemental;
            supplemental = 0;
        }

        // What is reported as worked out on: the income before a basic exemption.
        var reported = regular + supplemental;

        // A threshold taxes only the part of the year's income over it.
        if (rule.WageThreshold is { } threshold)
        {
            var before = request.YearIncome.TryGetValue(rule.Category, out var yearIncome) ? yearIncome : 0;
            var total = regular + supplemental;

            regular = Math.Max(0, before + total - threshold) - Math.Max(0, before - threshold);
            supplemental = 0;
            reported = regular;
        }

        // A wage base taxes only the income left under it this year: regular income first, then supplemental.
        if (rule.WageBase is { } wageBase)
        {
            var room = Math.Max(0, wageBase - toDate.YearTaxableIncome);

            regular = Math.Min(regular, room);
            supplemental = Math.Min(supplemental, room - regular);
            reported = regular + supplemental;
        }

        // A yearly exemption is taken off each pay in proportion to the periods of a year (regular income first), cut to
        // cents as the CRA and Revenu Québec do (3,500 ÷ 26 = 134.61).
        if (rule.YearlyExemption is { } exemption && exemption > 0)
        {
            var share = Math.Truncate(exemption / periods * 100m) / 100m;
            var fromRegular = Math.Min(regular, share);

            regular -= fromRegular;

            if (!ownSupplemental)
            {
                supplemental = Math.Max(0, supplemental - (share - fromRegular));
            }
        }

        // A second payment in a period (a bonus paid on its own, a correction) is withheld on the period's total less
        // what the earlier payments of the period were withheld on, as an income tax table expects a whole period.
        // Income an earlier payment withheld on at the flat supplemental rate is not part of that total.
        var prior = (rule.Annualize || withholding is not null) && !ownSupplemental
            ? Math.Max(0, toDate.PeriodTaxableIncome - toDate.PeriodFlatIncome)
            : 0;
        var tax = 0m;

        if (ownSupplemental)
        {
            if (regular > 0 || supplemental > 0)
            {
                tax = await WithholdAsync(rule, withholding, request, election, regular, supplemental, others, cancellationToken);
            }
        }
        else
        {
            if (regular > 0)
            {
                tax = prior > 0
                    ? Math.Max(0, await RegularAsync(rule, withholding, request, election, prior + regular, others, cancellationToken) - await RegularAsync(rule, withholding, request, election, prior, others, cancellationToken))
                    : await RegularAsync(rule, withholding, request, election, regular, others, cancellationToken);
            }

            if (supplemental > 0)
            {
                tax += supplemental * rule.SupplementalRate.Value / 100m;
            }
        }

        // Extra withholding is once a period, with the income the tables withhold on: never on supplemental income
        // withheld on at the flat rate.
        if (election is not null && prior == 0 && decimal.TryParse(election.Value("ExtraWithholding"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var extra) && regular > 0)
        {
            tax += extra;
        }

        tax += excessTax;

        if (rule.YearlyMaximum is { } maximum)
        {
            tax = Math.Min(tax, Math.Max(0, maximum - toDate.YearTax));
        }

        var taxable = reported + excess;

        if ((tax <= 0 && taxable <= 0) || (rule.WageThreshold is not null && tax <= 0))
        {
            return null;
        }

        return Line(rule, taxable, tax, flat ? supplemental + excess : excess);
    }

    private static IncomeTaxLine Line(IncomeTaxRule rule, decimal taxableIncome, decimal tax, decimal flatIncome)
        => new()
        {
            FlatIncome = flatIncome,
            TaxCode = rule.TaxCode,
            Name = rule.Name,
            Jurisdiction = rule.Jurisdiction,
            CollectedBy = rule.CollectedBy ?? rule.Jurisdiction,
            Category = rule.Category,
            Payer = rule.Payer,
            TaxableIncome = taxableIncome,
            Amount = Math.Max(0, tax),
        };

    // The tax of a period's regular income: by the withholding method, or by Taxation's method (annualized, less
    // allowances).
    private async Task<decimal> RegularAsync(IncomeTaxRule rule, IIncomeTaxWithholdingMethod withholding, IncomeTaxRequest request, IncomeTaxElection election, decimal regular, IReadOnlyList<IncomeTaxLine> others, CancellationToken cancellationToken)
    {
        if (regular <= 0)
        {
            return 0;
        }

        var periods = Math.Max(1, request.PeriodsPerYear);

        if (withholding is not null)
        {
            return await WithholdAsync(rule, withholding, request, election, regular, 0, others, cancellationToken);
        }

        var allowances = election is not null && decimal.TryParse(election.Value("Allowances"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? count * (rule.AllowanceAmount ?? 0)
            : 0;

        return rule.Annualize
            ? await ComputeAsync(rule, Math.Max(0, (regular * periods) - allowances), cancellationToken) / periods
            : await ComputeAsync(rule, Math.Max(0, regular - (allowances / periods)), cancellationToken);
    }

    private async Task<decimal> WithholdAsync(IncomeTaxRule rule, IIncomeTaxWithholdingMethod withholding, IncomeTaxRequest request, IncomeTaxElection election, decimal regular, decimal supplemental, IReadOnlyList<IncomeTaxLine> others, CancellationToken cancellationToken)
        => Math.Max(0, await withholding.CalculateAsync(new IncomeTaxWithholdingContext
        {
            Rule = rule,
            Request = request,
            Election = election,
            Income = regular,
            SupplementalIncome = supplemental,
            PeriodsPerYear = Math.Max(1, request.PeriodsPerYear),
            OtherTaxes = others,
            ComputeTableAsync = (key, amount, token) => ComputeTableAsync(rule, key, amount, token),
            LowestRateAsync = (key, token) => LowestRateAsync(rule, key, token),
            RateAtAsync = (key, amount, token) => RateAtAsync(rule, key, amount, token),
        }, cancellationToken));

    private async Task<TaxTable> TableAsync(IncomeTaxRule rule, string key, CancellationToken cancellationToken)
    {
        var tableId = rule.Tables?.GetValueOrDefault(key);
        var table = string.IsNullOrEmpty(tableId) ? null : await _tables.FindByIdAsync(tableId, cancellationToken);

        return table ?? throw new InvalidOperationException(S["The income tax {0} names no tax table for {1}.", rule.TaxCode, key]);
    }

    // Works out one of a withholding method's tables as progressive brackets.
    private async Task<decimal> ComputeTableAsync(IncomeTaxRule rule, string key, decimal amount, CancellationToken cancellationToken)
    {
        var table = await TableAsync(rule, key, cancellationToken);
        var method = _methods.GetMethod(TaxCalculationMethodNames.Progressive) ?? throw new InvalidOperationException(S["Taxation's progressive calculation method is not there."]);

        return method.Compute(new TaxComputationRequest { TaxableBase = Math.Max(0, amount), Table = table }).TaxAmount;
    }

    // The rate of a table's lowest bracket: credits are worked out at it.
    private async Task<decimal> LowestRateAsync(IncomeTaxRule rule, string key, CancellationToken cancellationToken)
        => (await TableAsync(rule, key, cancellationToken)).Rows.OrderBy(row => row.Minimum).Select(row => row.Rate).FirstOrDefault(rate => rate > 0);

    // The rate of the bracket an amount falls in: the marginal rate.
    private async Task<decimal> RateAtAsync(IncomeTaxRule rule, string key, decimal amount, CancellationToken cancellationToken)
        => (await TableAsync(rule, key, cancellationToken)).Rows
            .Where(row => amount > row.Minimum || (row.Minimum == 0 && amount >= 0))
            .OrderByDescending(row => row.Minimum)
            .Select(row => row.Rate)
            .FirstOrDefault();

    private async Task<decimal> ComputeAsync(IncomeTaxRule rule, decimal income, CancellationToken cancellationToken)
    {
        var method = _methods.GetMethod(rule.Method) ?? throw new InvalidOperationException(S["The income tax {0} names a calculation method that is not there: {1}.", rule.TaxCode, rule.Method]);
        var table = string.IsNullOrEmpty(rule.TaxTableId) ? null : await _tables.FindByIdAsync(rule.TaxTableId, cancellationToken);

        if (method.Inputs.HasFlag(TaxCalculationMethodInputs.TaxTable) && table is null)
        {
            throw new InvalidOperationException(S["The income tax {0} names a tax table that is not there.", rule.TaxCode]);
        }

        return method.Compute(new TaxComputationRequest
        {
            TaxableBase = income,
            Rate = rule.Rate / 100m,
            FixedAmount = rule.FixedAmount,
            Table = table,
        }).TaxAmount;
    }
}

/// <summary>
/// Keeps a Taxation tax table that income tax rules use from being deleted.
/// </summary>
public sealed class IncomeTaxRuleTableUsage : ITaxTableUsage
{
    private readonly IIncomeTaxRuleManager _rules;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="IncomeTaxRuleTableUsage"/> class.
    /// </summary>
    /// <param name="rules">The income tax rules.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public IncomeTaxRuleTableUsage(IIncomeTaxRuleManager rules, IStringLocalizer<IncomeTaxRuleTableUsage> stringLocalizer)
    {
        _rules = rules;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetUsagesAsync(TaxTable table, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);

        return [.. (await _rules.ListAsync(cancellationToken))
            .Where(rule => string.Equals(rule.TaxTableId, table.ItemId, StringComparison.OrdinalIgnoreCase)
                || (rule.Tables?.Values.Any(id => string.Equals(id, table.ItemId, StringComparison.OrdinalIgnoreCase)) ?? false))
            .Select(rule => S["the income tax rule {0}", rule.TaxCode].Value)];
    }
}

/// <summary>
/// Registers income tax rules and the built-in income tax provider.
/// </summary>
public static class IncomeTaxCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the rules, the built-in provider, the tax categories every country has and the election fields of any
    /// jurisdiction, on top of Taxation's tables. Calling it twice adds nothing twice.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddIncomeTaxCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(IncomeTaxMarker)))
        {
            return services;
        }

        services.AddSingleton<IncomeTaxMarker>();
        services.AddTaxTablesCore();
        services.TryAddScoped<IncomeTaxRuleStore>();
        services.TryAddScoped<IIncomeTaxRuleManager, IncomeTaxRuleManager>();
        services.TryAddScoped<ICatalogManager<IncomeTaxRule>>(serviceProvider => serviceProvider.GetRequiredService<IIncomeTaxRuleManager>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IIncomeTaxProvider, IncomeTaxRulesProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITaxTableUsage, IncomeTaxRuleTableUsage>());

        // Any jurisdiction gets the usual withholding fields until a country pack gives its own form.
        services.AddElectionFieldSet(GenericElectionFields.Create());

        // The tax categories every country has; a country pack adds its own.
        services.AddIncomeTaxCategory(IncomeTaxCategories.IncomeTax, LocalizationSource.Create("Income tax", typeof(IncomeTaxCoreServiceCollectionExtensions)));
        services.AddIncomeTaxCategory(IncomeTaxCategories.SocialInsurance, LocalizationSource.Create("Social insurance (pensionable earnings)", typeof(IncomeTaxCoreServiceCollectionExtensions)));
        services.AddIncomeTaxCategory(IncomeTaxCategories.Unemployment, LocalizationSource.Create("Unemployment insurance (insurable earnings)", typeof(IncomeTaxCoreServiceCollectionExtensions)));

        return services;
    }

    private sealed class IncomeTaxMarker;
}
