using System.Globalization;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Core.Canada;

/// <summary>
/// What the CRA's federal and provincial formulas share (T4127, Chapter 4, Option 1): the annual taxable income
/// A = P × (I − F5A) − HD − F1, with pre-tax deductions (F, F2, U1) already out of the period's income, the CPP or QPP
/// enhancement deduction split between periodic pay and a bonus, the credits for the pay's contributions, and the bonus
/// method (the year's tax with and without the bonus).
/// </summary>
internal sealed class CanadaPay
{
    private CanadaPay()
    {
    }

    /// <summary>Gets the pay periods in a year (P).</summary>
    public int Periods { get; private init; }

    /// <summary>Gets the period's regular income (I).</summary>
    public decimal Income { get; private init; }

    /// <summary>Gets the payment's bonus or retroactive pay (B).</summary>
    public decimal Bonus { get; private init; }

    /// <summary>Gets the bonuses paid earlier in the year (B1).</summary>
    public decimal YearBonuses { get; private init; }

    /// <summary>Gets whether the person works in Quebec (QPP, QPIP and the federal abatement).</summary>
    public bool Quebec { get; private init; }

    /// <summary>Gets the CPP or QPP enhancement deduction from periodic pay (F5A or F5Q's periodic part).</summary>
    public decimal PeriodicEnhancement { get; private init; }

    /// <summary>Gets the CPP or QPP enhancement deduction from the bonus (F5B).</summary>
    public decimal BonusEnhancement { get; private init; }

    /// <summary>Gets the base pension contribution of periodic pay, before the cap, for a year (P × C × base ÷ rate).</summary>
    public decimal YearBasePension { get; private init; }

    /// <summary>Gets the EI premium of periodic pay for a year (P × EI).</summary>
    public decimal YearEmploymentInsurance { get; private init; }

    /// <summary>Gets the QPIP premium of periodic pay for a year (P × QPIP).</summary>
    public decimal YearParentalInsurance { get; private init; }

    /// <summary>Gets the annual deduction for a prescribed zone (HD).</summary>
    public decimal PrescribedZone { get; private init; }

    /// <summary>Gets the other annual deductions authorized (F1).</summary>
    public decimal AnnualDeductions { get; private init; }

    /// <summary>Gets the annual taxable income of periodic pay (A).</summary>
    public decimal AnnualIncome
        => (Periods * (Income - PeriodicEnhancement)) - PrescribedZone - AnnualDeductions;

    /// <summary>Gets the annual taxable income with the bonuses of the year before this one (step 2 of the bonus method).</summary>
    public decimal AnnualIncomeWithEarlierBonuses
        => AnnualIncome + YearBonuses;

    /// <summary>Gets the annual taxable income with this bonus too (step 1 of the bonus method).</summary>
    public decimal AnnualIncomeWithBonus
        => AnnualIncomeWithEarlierBonuses + Bonus - BonusEnhancement;

    /// <summary>
    /// Reads a payment for the CRA's formulas.
    /// </summary>
    /// <param name="context">The withholding context.</param>
    /// <returns>The payment.</returns>
    public static CanadaPay From(IncomeTaxWithholdingContext context)
    {
        var request = context.Request;
        var quebec = WorksInQuebec(request);
        var periods = Math.Max(1, context.PeriodsPerYear);
        var rate = quebec ? context.Parameter(CanadaIncomeTax.Parameters.QppRate, 0.0630m) : context.Parameter(CanadaIncomeTax.Parameters.CppRate, 0.0595m);
        var baseRate = quebec ? context.Parameter(CanadaIncomeTax.Parameters.QppBaseRate, 0.0530m) : context.Parameter(CanadaIncomeTax.Parameters.CppBaseRate, 0.0495m);
        var contribution = context.OtherTax(quebec ? CanadaIncomeTax.TaxCodes.Qpp : CanadaIncomeTax.TaxCodes.Cpp);
        var second = context.OtherTax(quebec ? CanadaIncomeTax.TaxCodes.Qpp2 : CanadaIncomeTax.TaxCodes.Cpp2);
        var insurance = context.OtherTax(quebec ? CanadaIncomeTax.TaxCodes.EmploymentInsuranceQuebec : CanadaIncomeTax.TaxCodes.EmploymentInsurance);
        var parental = quebec ? context.OtherTax(CanadaIncomeTax.TaxCodes.Qpip) : 0m;

        // The part of the pay's contributions that comes from periodic pay, by the share of pensionable or insurable
        // earnings the bonus is (F5A = F5 × (PI − B) ÷ PI).
        var pensionable = Total(request, IncomeTaxCategories.SocialInsurance, context.Income + context.SupplementalIncome);
        var insurable = Total(request, CanadaIncomeTax.Categories.InsurableEarnings, context.Income + context.SupplementalIncome);
        var bonus = context.SupplementalIncome;
        var pensionShare = pensionable > 0 ? Math.Max(0, pensionable - bonus) / pensionable : 1m;
        var insuranceShare = insurable > 0 ? Math.Max(0, insurable - bonus) / insurable : 1m;
        var enhancement = (rate > 0 ? contribution * ((rate - baseRate) / rate) : 0m) + second;
        var federal = request.Elections.FirstOrDefault(election => string.Equals(election.Jurisdiction, CanadaIncomeTax.Jurisdiction, StringComparison.OrdinalIgnoreCase));

        return new CanadaPay
        {
            Periods = periods,
            Income = context.Income,
            Bonus = bonus,
            YearBonuses = bonus > 0 && request.YearSupplementalIncome.TryGetValue(IncomeTaxCategories.IncomeTax, out var earlier) ? earlier : 0m,
            Quebec = quebec,
            PeriodicEnhancement = enhancement * pensionShare,
            BonusEnhancement = enhancement * (1 - pensionShare),
            YearBasePension = rate > 0 ? periods * contribution * pensionShare * (baseRate / rate) : 0m,
            YearEmploymentInsurance = periods * insurance * insuranceShare,
            YearParentalInsurance = periods * parental * insuranceShare,
            PrescribedZone = Amount(federal, CanadaIncomeTax.Fields.PrescribedZone),
            AnnualDeductions = Amount(federal, CanadaIncomeTax.Fields.AnnualDeductions),
        };
    }

    /// <summary>
    /// Gets whether the person's province of employment is Quebec: where they work when that is known, else where they
    /// live.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>Whether it is.</returns>
    public static bool WorksInQuebec(IncomeTaxRequest request)
        => (IncomeTaxRulesProvider.PlaceOfWork(request, CanadaIncomeTax.Quebec) ?? [.. request.HomeJurisdictions ?? []])
            .Contains(CanadaIncomeTax.Quebec, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a basic personal amount that falls with net income (the federal BPAF, Yukon's BPAYT, Manitoba's BPAMB), or
    /// the flat one, rounded to cents.
    /// </summary>
    /// <param name="context">The withholding context.</param>
    /// <param name="netIncome">The net income (A + HD).</param>
    /// <returns>The basic personal amount.</returns>
    public static decimal BasicAmount(IncomeTaxWithholdingContext context, decimal netIncome)
    {
        var amount = context.Parameter(CanadaIncomeTax.Parameters.BasicAmount);

        if (!context.Rule.Parameters.TryGetValue(CanadaIncomeTax.Parameters.BasicAmountReductionStart, out var start)
            || !context.Rule.Parameters.TryGetValue(CanadaIncomeTax.Parameters.BasicAmountReductionEnd, out var end)
            || end <= start)
        {
            return amount;
        }

        var minimum = context.Parameter(CanadaIncomeTax.Parameters.BasicAmountMinimum);

        if (netIncome <= start)
        {
            return amount;
        }

        if (netIncome >= end)
        {
            return minimum;
        }

        return Math.Round(amount - ((netIncome - start) * ((amount - minimum) / (end - start))), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Gets an amount of an election, or zero.
    /// </summary>
    /// <param name="election">The election.</param>
    /// <param name="name">The field.</param>
    /// <returns>The amount.</returns>
    public static decimal Amount(IncomeTaxElection election, string name)
        => AmountOrNull(election, name) ?? 0m;

    /// <summary>
    /// Gets an amount of an election, or <see langword="null"/> when none was given.
    /// </summary>
    /// <param name="election">The election.</param>
    /// <param name="name">The field.</param>
    /// <returns>The amount.</returns>
    public static decimal? AmountOrNull(IncomeTaxElection election, string name)
        => decimal.TryParse(election?.Value(name), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount) ? amount : null;

    private static decimal Total(IncomeTaxRequest request, string category, decimal fallback)
    {
        var regular = request.Income.TryGetValue(category, out var income) ? income : (decimal?)null;
        var supplemental = request.SupplementalIncome.TryGetValue(category, out var extra) ? extra : 0m;

        return regular is { } value ? value + supplemental : fallback;
    }
}

/// <summary>
/// Federal income tax by the CRA's formulas (T4127, Chapter 4, Option 1): T3 = R × A − K − K1 − K2 − K4 on the brackets
/// table, with K1 the TD1 claim (or the basic personal amount, which falls with net income) at the lowest rate, K2 the
/// credits for the pay's CPP or QPP base contribution, EI premium and QPIP premium, K4 the Canada employment amount;
/// Quebec's abatement takes its share of T3 off. A bonus is withheld on by the bonus method.
/// </summary>
public sealed class CanadaFederalWithholdingMethod : IIncomeTaxWithholdingMethod
{
    private const string BracketsTable = "Brackets";

    private static readonly LocalizationSource _displayName = LocalizationSource.Create<CanadaFederalWithholdingMethod>("Canada federal (CRA payroll deductions formulas)");

    private static readonly IReadOnlyList<IncomeTaxWithholdingInput> _tables =
    [
        new(BracketsTable, LocalizationSource.Create<CanadaFederalWithholdingMethod>("Federal brackets (R and K)")),
    ];

    private static readonly IReadOnlyList<IncomeTaxWithholdingInput> _parameters =
    [
        new(CanadaIncomeTax.Parameters.BasicAmount, LocalizationSource.Create<CanadaFederalWithholdingMethod>("Basic personal amount (BPAF), most")),
        new(CanadaIncomeTax.Parameters.BasicAmountMinimum, LocalizationSource.Create<CanadaFederalWithholdingMethod>("Basic personal amount, least")),
        new(CanadaIncomeTax.Parameters.BasicAmountReductionStart, LocalizationSource.Create<CanadaFederalWithholdingMethod>("Net income the basic personal amount starts falling at")),
        new(CanadaIncomeTax.Parameters.BasicAmountReductionEnd, LocalizationSource.Create<CanadaFederalWithholdingMethod>("Net income the basic personal amount stops falling at")),
        new(CanadaIncomeTax.Parameters.CanadaEmploymentAmount, LocalizationSource.Create<CanadaFederalWithholdingMethod>("Canada employment amount (CEA)")),
        new(CanadaIncomeTax.Parameters.CppRate, LocalizationSource.Create<CanadaFederalWithholdingMethod>("CPP rate, base and first additional (fraction)")),
        new(CanadaIncomeTax.Parameters.CppBaseRate, LocalizationSource.Create<CanadaFederalWithholdingMethod>("CPP base rate (fraction)")),
        new(CanadaIncomeTax.Parameters.CppBaseMaximum, LocalizationSource.Create<CanadaFederalWithholdingMethod>("Base CPP maximum")),
        new(CanadaIncomeTax.Parameters.EiMaximum, LocalizationSource.Create<CanadaFederalWithholdingMethod>("EI maximum premium")),
        new(CanadaIncomeTax.Parameters.QppRate, LocalizationSource.Create<CanadaFederalWithholdingMethod>("QPP rate, base and first additional (fraction)")),
        new(CanadaIncomeTax.Parameters.QppBaseRate, LocalizationSource.Create<CanadaFederalWithholdingMethod>("QPP base rate (fraction)")),
        new(CanadaIncomeTax.Parameters.QppBaseMaximum, LocalizationSource.Create<CanadaFederalWithholdingMethod>("Base QPP maximum")),
        new(CanadaIncomeTax.Parameters.EiQuebecMaximum, LocalizationSource.Create<CanadaFederalWithholdingMethod>("EI maximum premium in Quebec")),
        new(CanadaIncomeTax.Parameters.QpipMaximum, LocalizationSource.Create<CanadaFederalWithholdingMethod>("QPIP maximum premium")),
        new(CanadaIncomeTax.Parameters.QuebecAbatement, LocalizationSource.Create<CanadaFederalWithholdingMethod>("Quebec abatement (fraction of basic federal tax)")),
    ];

    /// <inheritdoc />
    public string Name => CanadaIncomeTax.FederalMethod;

    /// <inheritdoc />
    public LocalizationSource DisplayName => _displayName;

    /// <inheritdoc />
    public IReadOnlyList<IncomeTaxWithholdingInput> Tables => _tables;

    /// <inheritdoc />
    public IReadOnlyList<IncomeTaxWithholdingInput> Parameters => _parameters;

    /// <inheritdoc />
    public bool HandlesSupplementalIncome => true;

    /// <inheritdoc />
    public bool UsesOtherTaxes => true;

    /// <inheritdoc />
    public async Task<decimal> CalculateAsync(IncomeTaxWithholdingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pay = CanadaPay.From(context);
        var lowest = await context.LowestRateAsync(BracketsTable, cancellationToken);
        var tax = pay.Income > 0 ? await YearTaxAsync(context, pay, lowest, pay.AnnualIncome, cancellationToken) / pay.Periods : 0m;

        if (pay.Bonus > 0)
        {
            tax += Math.Max(0, await YearTaxAsync(context, pay, lowest, pay.AnnualIncomeWithBonus, cancellationToken) - await YearTaxAsync(context, pay, lowest, pay.AnnualIncomeWithEarlierBonuses, cancellationToken));
        }

        return Math.Max(0, tax);
    }

    // T1: basic federal tax on an annual taxable income, less the Quebec abatement.
    private static async Task<decimal> YearTaxAsync(IncomeTaxWithholdingContext context, CanadaPay pay, decimal lowest, decimal annual, CancellationToken cancellationToken)
    {
        var claim = context.AmountOrNull(CanadaIncomeTax.Fields.ClaimAmount) ?? CanadaPay.BasicAmount(context, annual + pay.PrescribedZone);
        var k1 = lowest * claim;
        var k2 = pay.Quebec
            ? (lowest * Math.Min(pay.YearBasePension, context.Parameter(CanadaIncomeTax.Parameters.QppBaseMaximum, decimal.MaxValue)))
                + (lowest * Math.Min(pay.YearEmploymentInsurance, context.Parameter(CanadaIncomeTax.Parameters.EiQuebecMaximum, decimal.MaxValue)))
                + (lowest * Math.Min(pay.YearParentalInsurance, context.Parameter(CanadaIncomeTax.Parameters.QpipMaximum, decimal.MaxValue)))
            : (lowest * Math.Min(pay.YearBasePension, context.Parameter(CanadaIncomeTax.Parameters.CppBaseMaximum, decimal.MaxValue)))
                + (lowest * Math.Min(pay.YearEmploymentInsurance, context.Parameter(CanadaIncomeTax.Parameters.EiMaximum, decimal.MaxValue)));
        var k4 = Math.Min(lowest * pay.Periods * pay.Income, lowest * context.Parameter(CanadaIncomeTax.Parameters.CanadaEmploymentAmount));
        var basic = Math.Max(0, await context.ComputeTableAsync(BracketsTable, Math.Max(0, annual), cancellationToken) - k1 - k2 - k4);

        return pay.Quebec ? basic - (context.Parameter(CanadaIncomeTax.Parameters.QuebecAbatement) * basic) : basic;
    }
}

/// <summary>
/// Provincial and territorial income tax by the CRA's formulas (T4127, Steps 4 and 5) for the provinces it collects for:
/// T4 = V × A − KP − K1P − K2P − K4P − K5P on the brackets table, then T2 = T4 + V1 + V2 − S. Every province's own item
/// comes from the rule's parameters, so a province is set up without code: a basic personal amount that falls with net
/// income (Manitoba, Yukon), a supplemental credit (Alberta's K5P), the Canada employment amount (Yukon's K4P), a surtax
/// (Ontario's V1), a health premium table (Ontario's V2), a tax reduction for dependants (Ontario's S) or one that falls
/// with income (British Columbia's S). A bonus is withheld on by the bonus method.
/// </summary>
public sealed class CanadaProvincialWithholdingMethod : IIncomeTaxWithholdingMethod
{
    private const string BracketsTable = "Brackets";

    private const string HealthPremiumTable = "HealthPremium";

    private static readonly LocalizationSource _displayName = LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Canada provincial or territorial (CRA payroll deductions formulas)");

    private static readonly IReadOnlyList<IncomeTaxWithholdingInput> _tables =
    [
        new(BracketsTable, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Provincial brackets (V and KP)")),
        new(HealthPremiumTable, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Health premium (V2), by annual taxable income; Ontario only"), Required: false),
    ];

    private static readonly IReadOnlyList<IncomeTaxWithholdingInput> _parameters =
    [
        new(CanadaIncomeTax.Parameters.BasicAmount, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Basic personal amount (TCP when no TD1 was filed), most")),
        new(CanadaIncomeTax.Parameters.BasicAmountMinimum, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Basic personal amount, least (when it falls with net income)")),
        new(CanadaIncomeTax.Parameters.BasicAmountReductionStart, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Net income the basic personal amount starts falling at")),
        new(CanadaIncomeTax.Parameters.BasicAmountReductionEnd, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Net income the basic personal amount stops falling at")),
        new(CanadaIncomeTax.Parameters.CppRate, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("CPP rate, base and first additional (fraction)")),
        new(CanadaIncomeTax.Parameters.CppBaseRate, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("CPP base rate (fraction)")),
        new(CanadaIncomeTax.Parameters.CppBaseMaximum, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Base CPP maximum")),
        new(CanadaIncomeTax.Parameters.EiMaximum, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("EI maximum premium")),
        new(CanadaIncomeTax.Parameters.CanadaEmploymentAmount, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Canada employment amount for K4P (Yukon)")),
        new(CanadaIncomeTax.Parameters.SupplementalCreditThreshold, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Supplemental credit (K5P): credits it starts above (Alberta)")),
        new(CanadaIncomeTax.Parameters.SupplementalCreditRate, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Supplemental credit (K5P): rate (fraction)")),
        new(CanadaIncomeTax.Parameters.SurtaxThreshold1, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Surtax (V1): first threshold of basic tax (Ontario)")),
        new(CanadaIncomeTax.Parameters.SurtaxRate1, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Surtax: rate above the first threshold (fraction)")),
        new(CanadaIncomeTax.Parameters.SurtaxThreshold2, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Surtax: second threshold")),
        new(CanadaIncomeTax.Parameters.SurtaxRate2, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Surtax: rate added above the second threshold (fraction)")),
        new(CanadaIncomeTax.Parameters.DependantTaxReductionBase, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Tax reduction (S): basic amount (Ontario)")),
        new(CanadaIncomeTax.Parameters.DependantTaxReductionPerDependant, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Tax reduction: amount for each dependant")),
        new(CanadaIncomeTax.Parameters.IncomeTaxReductionAmount, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Tax reduction (S): amount (British Columbia)")),
        new(CanadaIncomeTax.Parameters.IncomeTaxReductionThreshold, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Tax reduction: income it starts falling at")),
        new(CanadaIncomeTax.Parameters.IncomeTaxReductionRate, LocalizationSource.Create<CanadaProvincialWithholdingMethod>("Tax reduction: how fast it falls (fraction)")),
    ];

    /// <inheritdoc />
    public string Name => CanadaIncomeTax.ProvincialMethod;

    /// <inheritdoc />
    public LocalizationSource DisplayName => _displayName;

    /// <inheritdoc />
    public IReadOnlyList<IncomeTaxWithholdingInput> Tables => _tables;

    /// <inheritdoc />
    public IReadOnlyList<IncomeTaxWithholdingInput> Parameters => _parameters;

    /// <inheritdoc />
    public bool HandlesSupplementalIncome => true;

    /// <inheritdoc />
    public bool UsesOtherTaxes => true;

    /// <inheritdoc />
    public async Task<decimal> CalculateAsync(IncomeTaxWithholdingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pay = CanadaPay.From(context);
        var lowest = await context.LowestRateAsync(BracketsTable, cancellationToken);
        var tax = pay.Income > 0 ? await YearTaxAsync(context, pay, lowest, pay.AnnualIncome, cancellationToken) / pay.Periods : 0m;

        if (pay.Bonus > 0)
        {
            tax += Math.Max(0, await YearTaxAsync(context, pay, lowest, pay.AnnualIncomeWithBonus, cancellationToken) - await YearTaxAsync(context, pay, lowest, pay.AnnualIncomeWithEarlierBonuses, cancellationToken));
        }

        return Math.Max(0, tax);
    }

    // T2: provincial tax on an annual taxable income.
    private static async Task<decimal> YearTaxAsync(IncomeTaxWithholdingContext context, CanadaPay pay, decimal lowest, decimal annual, CancellationToken cancellationToken)
    {
        var parameters = context.Rule.Parameters ?? [];
        var claim = context.AmountOrNull(CanadaIncomeTax.Fields.ClaimAmount) ?? CanadaPay.BasicAmount(context, annual + pay.PrescribedZone);
        var k1 = lowest * claim;
        var k2 = (lowest * Math.Min(pay.YearBasePension, context.Parameter(CanadaIncomeTax.Parameters.CppBaseMaximum, decimal.MaxValue)))
            + (lowest * Math.Min(pay.YearEmploymentInsurance, context.Parameter(CanadaIncomeTax.Parameters.EiMaximum, decimal.MaxValue)));
        var k4 = parameters.TryGetValue(CanadaIncomeTax.Parameters.CanadaEmploymentAmount, out var employmentAmount)
            ? Math.Min(lowest * pay.Periods * pay.Income, lowest * employmentAmount)
            : 0m;
        var k5 = parameters.TryGetValue(CanadaIncomeTax.Parameters.SupplementalCreditThreshold, out var creditThreshold)
            ? Math.Max(0, (k1 + k2 - creditThreshold) * context.Parameter(CanadaIncomeTax.Parameters.SupplementalCreditRate))
            : 0m;
        var basic = Math.Max(0, await context.ComputeTableAsync(BracketsTable, Math.Max(0, annual), cancellationToken) - k1 - k2 - k4 - k5);

        // V1: a surtax on basic provincial tax.
        var surtax = parameters.TryGetValue(CanadaIncomeTax.Parameters.SurtaxThreshold1, out var surtaxThreshold)
            ? (context.Parameter(CanadaIncomeTax.Parameters.SurtaxRate1) * Math.Max(0, basic - surtaxThreshold))
                + (context.Parameter(CanadaIncomeTax.Parameters.SurtaxRate2) * Math.Max(0, basic - context.Parameter(CanadaIncomeTax.Parameters.SurtaxThreshold2, decimal.MaxValue)))
            : 0m;

        // V2: a health premium on annual taxable income.
        var premium = !string.IsNullOrEmpty(context.Rule.Tables?.GetValueOrDefault(HealthPremiumTable))
            ? await context.ComputeTableAsync(HealthPremiumTable, Math.Max(0, annual), cancellationToken)
            : 0m;

        // S: a tax reduction, for dependants or falling with income.
        var reduction = 0m;

        if (parameters.TryGetValue(CanadaIncomeTax.Parameters.DependantTaxReductionBase, out var reductionBase))
        {
            var dependants = Math.Max(0, CanadaPay.Amount(context.Election, CanadaIncomeTax.Fields.Dependants));
            var allowed = 2 * (reductionBase + (dependants * context.Parameter(CanadaIncomeTax.Parameters.DependantTaxReductionPerDependant)));

            reduction = Math.Max(0, Math.Min(basic + surtax, allowed - (basic + surtax)));
        }
        else if (parameters.TryGetValue(CanadaIncomeTax.Parameters.IncomeTaxReductionAmount, out var reductionAmount))
        {
            var threshold = context.Parameter(CanadaIncomeTax.Parameters.IncomeTaxReductionThreshold);

            reduction = Math.Min(basic, Math.Max(0, reductionAmount - (Math.Max(0, annual - threshold) * context.Parameter(CanadaIncomeTax.Parameters.IncomeTaxReductionRate))));
        }

        return Math.Max(0, basic + surtax + premium - reduction);
    }
}

/// <summary>
/// Quebec income tax by Revenu Québec's formulas (TP-1015.F, part 2.1): I = P × (G − H − CSA) − J, Y = T × I − K − the
/// TP-1015.3 claim (or the basic personal amount) at the lowest rate, with H the deduction for workers and CSA the QPP
/// enhancement deduction of periodic pay. A lump sum (bonus, retroactive pay) is withheld at the marginal rate (the second
/// method of 2.1.2), or at the flat lump sum rate when the year's income with it is at or below the threshold.
/// </summary>
public sealed class QuebecWithholdingMethod : IIncomeTaxWithholdingMethod
{
    private const string BracketsTable = "Brackets";

    private static readonly LocalizationSource _displayName = LocalizationSource.Create<QuebecWithholdingMethod>("Quebec (Revenu Québec TP-1015.F formulas)");

    private static readonly IReadOnlyList<IncomeTaxWithholdingInput> _tables =
    [
        new(BracketsTable, LocalizationSource.Create<QuebecWithholdingMethod>("Quebec brackets (T and K)")),
    ];

    private static readonly IReadOnlyList<IncomeTaxWithholdingInput> _parameters =
    [
        new(CanadaIncomeTax.Parameters.BasicAmount, LocalizationSource.Create<QuebecWithholdingMethod>("Basic personal amount (E1 when no TP-1015.3 was filed)")),
        new(CanadaIncomeTax.Parameters.WorkerDeductionRate, LocalizationSource.Create<QuebecWithholdingMethod>("Deduction for workers: rate (fraction)")),
        new(CanadaIncomeTax.Parameters.WorkerDeductionMaximum, LocalizationSource.Create<QuebecWithholdingMethod>("Deduction for workers: yearly maximum")),
        new(CanadaIncomeTax.Parameters.QppRate, LocalizationSource.Create<QuebecWithholdingMethod>("QPP rate, base and first additional (fraction)")),
        new(CanadaIncomeTax.Parameters.QppBaseRate, LocalizationSource.Create<QuebecWithholdingMethod>("QPP base rate (fraction)")),
        new(CanadaIncomeTax.Parameters.LumpSumThreshold, LocalizationSource.Create<QuebecWithholdingMethod>("Lump sums: the year's income at or below which the flat rate applies")),
        new(CanadaIncomeTax.Parameters.LumpSumRate, LocalizationSource.Create<QuebecWithholdingMethod>("Lump sums: the flat rate (fraction)")),
    ];

    /// <inheritdoc />
    public string Name => CanadaIncomeTax.QuebecMethod;

    /// <inheritdoc />
    public LocalizationSource DisplayName => _displayName;

    /// <inheritdoc />
    public IReadOnlyList<IncomeTaxWithholdingInput> Tables => _tables;

    /// <inheritdoc />
    public IReadOnlyList<IncomeTaxWithholdingInput> Parameters => _parameters;

    /// <inheritdoc />
    public bool HandlesSupplementalIncome => true;

    /// <inheritdoc />
    public bool UsesOtherTaxes => true;

    /// <inheritdoc />
    public async Task<decimal> CalculateAsync(IncomeTaxWithholdingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Request;
        var periods = Math.Max(1, context.PeriodsPerYear);
        var salary = context.Income;
        var lumpSum = context.SupplementalIncome;
        var earlierLumpSums = lumpSum > 0 && request.YearSupplementalIncome.TryGetValue(IncomeTaxCategories.IncomeTax, out var earlier) ? earlier : 0m;
        var rate = context.Parameter(CanadaIncomeTax.Parameters.QppRate, 0.0630m);
        var baseRate = context.Parameter(CanadaIncomeTax.Parameters.QppBaseRate, 0.0530m);

        // CS = C × (0.01 ÷ 0.0630) + C2, split between periodic pay (CSA) and the lump sum (CSB) by pensionable salary.
        var enhancement = (rate > 0 ? context.OtherTax(CanadaIncomeTax.TaxCodes.Qpp) * ((rate - baseRate) / rate) : 0m) + context.OtherTax(CanadaIncomeTax.TaxCodes.Qpp2);
        var pensionable = request.Income.TryGetValue(IncomeTaxCategories.SocialInsurance, out var regularPensionable)
            ? regularPensionable + (request.SupplementalIncome.TryGetValue(IncomeTaxCategories.SocialInsurance, out var supplementalPensionable) ? supplementalPensionable : 0m)
            : salary + lumpSum;
        var periodicEnhancement = pensionable > 0 ? enhancement * (Math.Max(0, pensionable - lumpSum) / pensionable) : enhancement;
        var lumpSumEnhancement = enhancement - periodicEnhancement;

        // H: the deduction for workers, 6% of the period's salary up to the year's maximum over the periods.
        var workerDeduction = Math.Min(context.Parameter(CanadaIncomeTax.Parameters.WorkerDeductionRate) * salary, context.Parameter(CanadaIncomeTax.Parameters.WorkerDeductionMaximum) / periods);
        var deductions = context.Amount(CanadaIncomeTax.Fields.AnnualDeductions);
        var claim = Math.Round(context.AmountOrNull(CanadaIncomeTax.Fields.ClaimAmount) ?? context.Parameter(CanadaIncomeTax.Parameters.BasicAmount), 0, MidpointRounding.AwayFromZero);
        var lowest = await context.LowestRateAsync(BracketsTable, cancellationToken);
        var annual = (periods * (salary - workerDeduction - periodicEnhancement)) - deductions;
        var tax = 0m;

        if (salary > 0)
        {
            tax = Math.Max(0, await context.ComputeTableAsync(BracketsTable, Math.Max(0, annual), cancellationToken) - (lowest * claim)) / periods;
        }

        if (lumpSum > 0)
        {
            if ((periods * salary) + earlierLumpSums + lumpSum <= context.Parameter(CanadaIncomeTax.Parameters.LumpSumThreshold))
            {
                tax += context.Parameter(CanadaIncomeTax.Parameters.LumpSumRate) * lumpSum;
            }
            else
            {
                var withLumpSum = Math.Max(0, annual + earlierLumpSums + lumpSum - lumpSumEnhancement);
                var marginal = await context.RateAtAsync(BracketsTable, withLumpSum, cancellationToken);

                tax += marginal * Math.Max(0, lumpSum - lumpSumEnhancement);
            }
        }

        return Math.Max(0, tax);
    }
}
