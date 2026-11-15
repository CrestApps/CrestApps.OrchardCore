using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Payroll.Core.Calculation;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation;
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

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Indexes payroll tax rules.
/// </summary>
public sealed class PayrollTaxRuleIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the tax's code.</summary>
    public string TaxCode { get; set; }

    /// <summary>Gets or sets the jurisdiction.</summary>
    public string Jurisdiction { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; }
}

/// <summary>
/// Maps payroll tax rules to <see cref="PayrollTaxRuleIndex"/> rows.
/// </summary>
public sealed class PayrollTaxRuleIndexProvider : IndexProvider<PayrollTaxRule>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollTaxRuleIndexProvider"/> class.
    /// </summary>
    public PayrollTaxRuleIndexProvider()
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PayrollTaxRule> context)
    {
        context
            .For<PayrollTaxRuleIndex>()
            .Map(rule => new PayrollTaxRuleIndex
            {
                ItemId = rule.ItemId,
                TaxCode = rule.TaxCode,
                Jurisdiction = rule.Jurisdiction,
                Name = rule.Name,
            });
    }
}

/// <summary>
/// Creates the table of payroll tax rules.
/// </summary>
public static class PayrollTaxRuleIndexSchema
{
    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<PayrollTaxRuleIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("TaxCode", column => column.Nullable().WithLength(50))
            .Column<string>("Jurisdiction", column => column.Nullable().WithLength(50))
            .Column<string>("Name", column => column.Nullable().WithLength(255)),
            collection: PayrollConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<PayrollTaxRuleIndex>(table => table
            .CreateIndex("IDX_PayrollTaxRuleIndex_ItemId", "ItemId", "DocumentId"),
            collection: PayrollConstants.CollectionName);
    }
}

/// <summary>
/// Stores payroll tax rules in the Payroll collection.
/// </summary>
public sealed class PayrollTaxRuleStore : DocumentCatalog<PayrollTaxRule, PayrollTaxRuleIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollTaxRuleStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public PayrollTaxRuleStore(ISession session)
        : base(session)
    {
        CollectionName = PayrollConstants.CollectionName;
    }
}

/// <summary>
/// The default <see cref="IPayrollTaxRuleManager"/>.
/// </summary>
public sealed class PayrollTaxRuleManager : CatalogManager<PayrollTaxRule>, IPayrollTaxRuleManager
{
    private readonly PayrollTaxRuleStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollTaxRuleManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public PayrollTaxRuleManager(PayrollTaxRuleStore store, IEnumerable<ICatalogEntryHandler<PayrollTaxRule>> handlers, ILogger<CatalogManager<PayrollTaxRule>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PayrollTaxRule>> ListAsync(CancellationToken cancellationToken = default)
        => [.. await _store.GetAllAsync(cancellationToken)];
}

/// <summary>
/// Stamps payroll tax rules and checks them: a code, a jurisdiction, a known method with what it needs, rates from 0 to
/// 100, amounts of zero or more, and no two versions of one tax in effect on the same day.
/// </summary>
public sealed class PayrollTaxRuleHandler : CatalogEntryHandlerBase<PayrollTaxRule>
{
    private readonly IClock _clock;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollTaxRuleHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="services">The services, for the calculation methods and the other rules.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayrollTaxRuleHandler(IClock clock, IServiceProvider services, IStringLocalizer<PayrollTaxRuleHandler> stringLocalizer)
    {
        _clock = clock;
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task InitializingAsync(InitializingContext<PayrollTaxRule> context, CancellationToken cancellationToken = default)
    {
        // A recipe or a deployment plan hands the values in; a page sets them itself and passes none.
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Normalize(context.Model);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task InitializedAsync(InitializedContext<PayrollTaxRule> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<PayrollTaxRule> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Normalize(context.Model);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    // Trims and upper-cases what a page, a recipe or a deployment plan typed.
    private static void Normalize(PayrollTaxRule rule)
    {
        rule.Name = rule.Name?.Trim();
        rule.TaxCode = rule.TaxCode?.Trim().ToUpperInvariant();
        rule.Jurisdiction = rule.Jurisdiction?.Trim().ToUpperInvariant();
    }

    /// <inheritdoc />
    public override async Task ValidatingAsync(ValidatingContext<PayrollTaxRule> context, CancellationToken cancellationToken = default)
    {
        var rule = context.Model;

        Normalize(rule);

        if (string.IsNullOrEmpty(rule.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(PayrollTaxRule.Name)]));
        }

        if (string.IsNullOrEmpty(rule.TaxCode))
        {
            context.Result.Fail(new ValidationResult(S["The tax code is required."], [nameof(PayrollTaxRule.TaxCode)]));
        }

        if (string.IsNullOrEmpty(rule.Jurisdiction))
        {
            context.Result.Fail(new ValidationResult(S["The jurisdiction is required."], [nameof(PayrollTaxRule.Jurisdiction)]));
        }

        if (string.IsNullOrEmpty(rule.Category))
        {
            context.Result.Fail(new ValidationResult(S["Choose the tax category whose wages are taxed."], [nameof(PayrollTaxRule.Category)]));
        }

        var withholding = _services.GetServices<IPayrollWithholdingMethod>().FirstOrDefault(candidate => string.Equals(candidate.Name, rule.Method, StringComparison.OrdinalIgnoreCase));
        var method = withholding is null ? _services.GetService<ITaxCalculationMethodProvider>()?.GetMethod(rule.Method) : null;

        if (withholding is not null)
        {
            foreach (var table in withholding.Tables.Where(table => string.IsNullOrEmpty(rule.Tables?.GetValueOrDefault(table.Key))))
            {
                context.Result.Fail(new ValidationResult(S["The method needs the tax table {0}.", table.Label.Value], [nameof(PayrollTaxRule.Tables)]));
            }
        }
        else if (method is null)
        {
            context.Result.Fail(new ValidationResult(S["Choose a calculation method."], [nameof(PayrollTaxRule.Method)]));
        }
        else
        {
            if (method.Inputs.HasFlag(TaxCalculationMethodInputs.Rate) && rule.Rate is null)
            {
                context.Result.Fail(new ValidationResult(S["The method needs a rate."], [nameof(PayrollTaxRule.Rate)]));
            }

            if (method.Inputs.HasFlag(TaxCalculationMethodInputs.FixedAmount) && rule.FixedAmount is null)
            {
                context.Result.Fail(new ValidationResult(S["The method needs a fixed amount."], [nameof(PayrollTaxRule.FixedAmount)]));
            }

            if (method.Inputs.HasFlag(TaxCalculationMethodInputs.TaxTable) && string.IsNullOrEmpty(rule.TaxTableId))
            {
                context.Result.Fail(new ValidationResult(S["The method needs a tax table."], [nameof(PayrollTaxRule.TaxTableId)]));
            }
        }

        if (rule.Rate is < 0 or > 100 || rule.SupplementalRate is < 0 or > 100 || rule.SupplementalExcessRate is < 0 or > 100)
        {
            context.Result.Fail(new ValidationResult(S["A rate is a percent from 0 to 100."], [nameof(PayrollTaxRule.Rate)]));
        }

        if (rule.FixedAmount is < 0 || rule.WageBase is < 0 || rule.AllowanceAmount is < 0 || rule.WageThreshold is < 0 || rule.SupplementalExcessThreshold is < 0)
        {
            context.Result.Fail(new ValidationResult(S["An amount is zero or more."], [nameof(PayrollTaxRule.WageBase)]));
        }

        if (rule.EffectiveTo is { } to && to < rule.EffectiveFrom)
        {
            context.Result.Fail(new ValidationResult(S["The last day is before the first day."], [nameof(PayrollTaxRule.EffectiveTo)]));
        }

        if (!string.IsNullOrEmpty(rule.TaxCode) && _services.GetService<PayrollTaxRuleStore>() is { } store)
        {
            var overlapping = (await store.GetAllAsync(cancellationToken)).FirstOrDefault(other => other.ItemId != rule.ItemId
                && other.TaxCode == rule.TaxCode
                && other.Enabled && rule.Enabled
                && other.EffectiveFrom <= (rule.EffectiveTo ?? DateOnly.MaxValue)
                && rule.EffectiveFrom <= (other.EffectiveTo ?? DateOnly.MaxValue));

            if (overlapping is not null)
            {
                context.Result.Fail(new ValidationResult(S["Another version of {0} is in effect on some of the same days ({1}). Give it a last day first.", rule.TaxCode, overlapping.Name], [nameof(PayrollTaxRule.EffectiveFrom)]));
            }
        }
    }
}

/// <summary>
/// The built-in tax provider: the payroll tax rules in effect on the pay date, worked out with Taxation's calculation
/// methods over its tax tables. A rule applies when its jurisdiction is one the employer is registered in and, for a
/// jurisdiction below a country (with a dash, such as US-CA), one of the person's home or work jurisdictions; a person
/// with neither is taxed by every registered jurisdiction.
/// </summary>
public sealed class TaxTablesPayrollTaxProvider : IPayrollTaxProvider
{
    /// <summary>
    /// The provider's stored name.
    /// </summary>
    public const string ProviderName = "TaxTables";

    private static readonly LocalizationSource _displayName = LocalizationSource.Create<TaxTablesPayrollTaxProvider>("Payroll tax tables");

    private readonly IPayrollTaxRuleManager _rules;
    private readonly ITaxCalculationMethodProvider _methods;
    private readonly INamedCatalog<TaxTable> _tables;
    private readonly IEnumerable<IPayrollWithholdingMethod> _withholdingMethods;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxTablesPayrollTaxProvider"/> class.
    /// </summary>
    /// <param name="rules">The rules.</param>
    /// <param name="methods">The calculation methods.</param>
    /// <param name="tables">The tax tables.</param>
    /// <param name="withholdingMethods">The withholding methods country packs add.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxTablesPayrollTaxProvider(IPayrollTaxRuleManager rules, ITaxCalculationMethodProvider methods, INamedCatalog<TaxTable> tables, IEnumerable<IPayrollWithholdingMethod> withholdingMethods, IStringLocalizer<TaxTablesPayrollTaxProvider> stringLocalizer)
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
    public async Task<PayrollTaxResult> CalculateAsync(PayrollTaxRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new PayrollTaxResult();

        // A contractor has only contractor withholding, and only when it applies; an employee never has it.
        var contractor = request.WorkerType == PayWorkerType.Contractor;

        if (contractor && !request.ContractorWithholding)
        {
            return result;
        }

        var registered = (request.Employer?.Registrations ?? [])
            .Select(registration => registration.Jurisdiction)
            .Where(jurisdiction => !string.IsNullOrEmpty(jurisdiction))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var personal = request.HomeJurisdictions.Concat(request.WorkJurisdictions).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rules = (await _rules.ListAsync(cancellationToken))
            .Where(rule => rule.AppliesOn(request.PayDate) && registered.Contains(rule.Jurisdiction)
                && contractor == (rule.Category == PayrollTaxCategories.ContractorWithholding)
                && (!rule.Jurisdiction.Contains('-', StringComparison.Ordinal) || personal.Count == 0 || personal.Contains(rule.Jurisdiction)))
            .OrderBy(rule => rule.Jurisdiction, StringComparer.Ordinal)
            .ThenBy(rule => rule.TaxCode, StringComparer.Ordinal);

        foreach (var rule in rules)
        {
            var withholding = WithholdingMethod(rule);
            List<string> tableIds = withholding is not null
                ? [.. withholding.Tables.Select(table => rule.Tables?.GetValueOrDefault(table.Key))]
                : _methods.GetMethod(rule.Method) is { } method && method.Inputs.HasFlag(TaxCalculationMethodInputs.TaxTable) ? [rule.TaxTableId] : [];
            var inEffect = true;
            var day = request.PayDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            foreach (var tableId in tableIds)
            {
                var table = string.IsNullOrEmpty(tableId) ? null : await _tables.FindByIdAsync(tableId, cancellationToken);

                inEffect &= table is not null && (table.EffectiveFromUtc is not { } from || from.Date <= day) && (table.EffectiveToUtc is not { } to || to.Date >= day);
            }

            if (!inEffect)
            {
                result.Warnings.Add(S["{0} was not worked out: its tax table is missing or not in effect on the pay date.", rule.Name]);

                continue;
            }

            var line = await CalculateAsync(rule, withholding, request, cancellationToken);

            if (line is not null)
            {
                result.Lines.Add(line);
            }
        }

        return result;
    }

    private IPayrollWithholdingMethod WithholdingMethod(PayrollTaxRule rule)
        => _withholdingMethods.FirstOrDefault(method => string.Equals(method.Name, rule.Method, StringComparison.OrdinalIgnoreCase));

    private async Task<PayrollTaxLine> CalculateAsync(PayrollTaxRule rule, IPayrollWithholdingMethod withholding, PayrollTaxRequest request, CancellationToken cancellationToken)
    {
        var election = rule.UsesElections
            ? request.Elections.FirstOrDefault(candidate => string.Equals(candidate.Jurisdiction, rule.Jurisdiction, StringComparison.OrdinalIgnoreCase))
            : null;
        var regular = request.Wages.TryGetValue(rule.Category, out var wages) ? wages : 0;
        var supplemental = request.SupplementalWages.TryGetValue(rule.Category, out var supplementalWages) ? supplementalWages : 0;

        // The part of the year's supplemental wages above a threshold is withheld at its own rate, whatever the
        // method and the election (such as a mandatory rate on supplemental wages over a million).
        var excess = 0m;

        if (rule.SupplementalExcessRate is not null && rule.SupplementalExcessThreshold is { } excessThreshold && supplemental > 0)
        {
            var before = request.YearSupplementalWages.TryGetValue(rule.Category, out var yearSupplemental) ? yearSupplemental : 0;

            excess = Math.Min(supplemental, Math.Max(0, before + supplemental - excessThreshold) - Math.Max(0, before - excessThreshold));
            supplemental -= excess;
        }

        var excessTax = excess * (rule.SupplementalExcessRate ?? 0) / 100m;

        if (election?.Values.GetValueOrDefault("Exempt") == "true")
        {
            return excess > 0 ? Line(rule, excess, excessTax) : null;
        }

        var flat = request.SupplementalMethod == PayrollSupplementalMethod.Flat && rule.SupplementalRate is not null;

        if (!flat)
        {
            regular += supplemental;
            supplemental = 0;
        }

        // A threshold taxes only the part of the year's wages over it.
        if (rule.WageThreshold is { } threshold)
        {
            var before = request.YearWages.TryGetValue(rule.Category, out var yearWages) ? yearWages : 0;
            var total = regular + supplemental;

            regular = Math.Max(0, before + total - threshold) - Math.Max(0, before - threshold);
            supplemental = 0;
        }

        // A wage base taxes only the wages left under it this year: regular wages first, then supplemental.
        if (rule.WageBase is { } wageBase)
        {
            var room = Math.Max(0, wageBase - request.Accumulators.For(PayAccumulators.TaxKey(rule.TaxCode)).YearTaxableWages);

            regular = Math.Min(regular, room);
            supplemental = Math.Min(supplemental, room - regular);
        }

        // A second pay in a period (a bonus paid on its own, a correction) is withheld on the period's total less
        // what the earlier pays of the period were withheld on, as an income tax table expects a whole period.
        var prior = rule.Annualize || withholding is not null
            ? Math.Max(0, request.Accumulators.For(PayAccumulators.TaxKey(rule.TaxCode)).PeriodTaxableWages)
            : 0;
        var tax = 0m;

        if (regular > 0)
        {
            tax = prior > 0
                ? Math.Max(0, await RegularAsync(rule, withholding, request, election, prior + regular, cancellationToken) - await RegularAsync(rule, withholding, request, election, prior, cancellationToken))
                : await RegularAsync(rule, withholding, request, election, regular, cancellationToken);
        }

        if (supplemental > 0)
        {
            tax += supplemental * rule.SupplementalRate.Value / 100m;
        }

        // Extra withholding is once a period.
        if (election is not null && prior == 0 && decimal.TryParse(election.Values.GetValueOrDefault("ExtraWithholding"), System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var extra) && regular + supplemental > 0)
        {
            tax += extra;
        }

        tax += excessTax;

        var taxable = regular + supplemental + excess;

        if ((tax <= 0 && taxable <= 0) || (rule.WageThreshold is not null && tax <= 0))
        {
            return null;
        }

        return Line(rule, taxable, tax);
    }

    private static PayrollTaxLine Line(PayrollTaxRule rule, decimal taxableWages, decimal tax)
        => new()
        {
            TaxCode = rule.TaxCode,
            Name = rule.Name,
            Jurisdiction = rule.Jurisdiction,
            Category = rule.Category,
            Payer = rule.Payer,
            TaxableWages = taxableWages,
            Amount = Math.Max(0, tax),
        };

    // The tax of a period's regular wages: by the withholding method, or by Taxation's method (annualized, less
    // allowances).
    private async Task<decimal> RegularAsync(PayrollTaxRule rule, IPayrollWithholdingMethod withholding, PayrollTaxRequest request, TaxElection election, decimal regular, CancellationToken cancellationToken)
    {
        if (regular <= 0)
        {
            return 0;
        }

        var periods = Math.Max(1, request.PeriodsPerYear);

        if (withholding is not null)
        {
            return Math.Max(0, await withholding.CalculateAsync(new PayrollWithholdingContext
            {
                Rule = rule,
                Request = request,
                Election = election,
                Wages = regular,
                PeriodsPerYear = periods,
                ComputeTableAsync = (key, amount, token) => ComputeTableAsync(rule, key, amount, token),
            }, cancellationToken));
        }

        var allowances = election is not null && decimal.TryParse(election.Values.GetValueOrDefault("Allowances"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var count)
            ? count * (rule.AllowanceAmount ?? 0)
            : 0;

        return rule.Annualize
            ? await ComputeAsync(rule, Math.Max(0, (regular * periods) - allowances), cancellationToken) / periods
            : await ComputeAsync(rule, Math.Max(0, regular - (allowances / periods)), cancellationToken);
    }

    // Works out one of a withholding method's tables as progressive brackets.
    private async Task<decimal> ComputeTableAsync(PayrollTaxRule rule, string key, decimal amount, CancellationToken cancellationToken)
    {
        var tableId = rule.Tables?.GetValueOrDefault(key);
        var table = string.IsNullOrEmpty(tableId) ? null : await _tables.FindByIdAsync(tableId, cancellationToken);

        if (table is null)
        {
            throw new InvalidOperationException(S["The payroll tax {0} names no tax table for {1}.", rule.TaxCode, key]);
        }

        var method = _methods.GetMethod(TaxCalculationMethodNames.Progressive) ?? throw new InvalidOperationException(S["Taxation's progressive calculation method is not there."]);

        return method.Compute(new TaxComputationRequest { TaxableBase = Math.Max(0, amount), Table = table }).TaxAmount;
    }

    private async Task<decimal> ComputeAsync(PayrollTaxRule rule, decimal wages, CancellationToken cancellationToken)
    {
        var method = _methods.GetMethod(rule.Method) ?? throw new InvalidOperationException(S["The payroll tax {0} names a calculation method that is not there: {1}.", rule.TaxCode, rule.Method]);
        var table = string.IsNullOrEmpty(rule.TaxTableId) ? null : await _tables.FindByIdAsync(rule.TaxTableId, cancellationToken);

        if (method.Inputs.HasFlag(TaxCalculationMethodInputs.TaxTable) && table is null)
        {
            throw new InvalidOperationException(S["The payroll tax {0} names a tax table that is not there.", rule.TaxCode]);
        }

        return method.Compute(new TaxComputationRequest
        {
            TaxableBase = wages,
            Rate = rule.Rate / 100m,
            FixedAmount = rule.FixedAmount,
            Table = table,
        }).TaxAmount;
    }
}

/// <summary>
/// Keeps a Taxation tax table that payroll tax rules use from being deleted.
/// </summary>
public sealed class PayrollTaxRuleTableUsage : ITaxTableUsage
{
    private readonly IPayrollTaxRuleManager _rules;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollTaxRuleTableUsage"/> class.
    /// </summary>
    /// <param name="rules">The payroll tax rules.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayrollTaxRuleTableUsage(IPayrollTaxRuleManager rules, IStringLocalizer<PayrollTaxRuleTableUsage> stringLocalizer)
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
            .Select(rule => S["the payroll tax rule {0}", rule.TaxCode].Value)];
    }
}

/// <summary>
/// Registers the payroll tax rules and the built-in tax provider.
/// </summary>
public static class PayrollTaxTablesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the rules, their handler and the Payroll Tax Tables provider, on top of Taxation's tables.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPayrollTaxTablesCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<PayrollTaxRuleStore>();
        services.TryAddScoped<IPayrollTaxRuleManager, PayrollTaxRuleManager>();
        services.TryAddScoped<ICatalogManager<PayrollTaxRule>>(serviceProvider => serviceProvider.GetRequiredService<IPayrollTaxRuleManager>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<PayrollTaxRule>, PayrollTaxRuleHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayrollTaxProvider, TaxTablesPayrollTaxProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITaxTableUsage, PayrollTaxRuleTableUsage>());

        return services;
    }
}
