using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// A tax on income the built-in income tax provider works out: a jurisdiction's tax, at any level (country, state or
/// province, local), on the income of one tax category, paid by the earner or the payer, by one of Taxation's
/// calculation methods (a rate, a fixed amount, a table or progressive brackets) or a withholding method a country pack
/// adds, from a day on. It covers income tax withholding and the statutory contributions on earnings (such as Social
/// Security and Medicare in the United States, or the Canada Pension Plan and Employment Insurance in Canada). Any
/// jurisdiction of any country can be set up by hand; a country pack ships its own as dated recipes.
/// </summary>
public sealed class IncomeTaxRule : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets what a statement shows, such as "State income tax".
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the tax's code, the same for every dated version of the tax (year-to-date totals, wage bases and
    /// maximums sum by it), such as US-CA-SIT or CA-CPP.
    /// </summary>
    public string TaxCode { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction, such as US, US-CA, CA or CA-QC. One with a dash applies only to a person in it.
    /// </summary>
    public string Jurisdiction { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction whose registration covers the tax, when it isn't the rule's own: a province's tax
    /// that the federal agency collects (CA for CA-ON). <see langword="null"/> means the rule's jurisdiction. A rule
    /// applies only to a payer registered in it.
    /// </summary>
    public string CollectedBy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a jurisdiction below a country is matched by where the person works when
    /// that is known, never by where they live (Canada's province of employment); otherwise home and work both count.
    /// </summary>
    public bool ByPlaceOfWork { get; set; }

    /// <summary>
    /// Gets or sets the jurisdictions whose people the rule doesn't apply to, such as CA-QC for the Canada Pension Plan
    /// (Quebec has its own plan).
    /// </summary>
    public List<string> ExceptJurisdictions { get; set; } = [];

    /// <summary>
    /// Gets or sets the tax category whose income is taxed.
    /// </summary>
    public string Category { get; set; } = IncomeTaxCategories.IncomeTax;

    /// <summary>
    /// Gets or sets the kinds of income the rule applies to. Empty means employment income, or a contractor's payments
    /// for a rule of the <see cref="IncomeTaxCategories.ContractorWithholding"/> category.
    /// </summary>
    public List<IncomeKind> IncomeKinds { get; set; } = [];

    /// <summary>
    /// Gets or sets who pays.
    /// </summary>
    public IncomeTaxPayer Payer { get; set; }

    /// <summary>
    /// Gets or sets the name of Taxation's calculation method, such as Percentage or Progressive, or of a withholding
    /// method a country pack adds (<c>IIncomeTaxWithholdingMethod</c>), such as UnitedStatesFormW4.
    /// </summary>
    public string Method { get; set; } = "Percentage";

    /// <summary>
    /// Gets or sets the rate, as a percent (6.2 for 6.2%), for a method that takes a rate.
    /// </summary>
    public decimal? Rate { get; set; }

    /// <summary>
    /// Gets or sets the fixed amount, for a method that takes one.
    /// </summary>
    public decimal? FixedAmount { get; set; }

    /// <summary>
    /// Gets or sets the Taxation tax table, for a method that takes a table (rates in its rows are fractions).
    /// </summary>
    public string TaxTableId { get; set; }

    /// <summary>
    /// Gets or sets the income taxed in a calendar year at most, or <see langword="null"/> for no limit.
    /// </summary>
    public decimal? WageBase { get; set; }

    /// <summary>
    /// Gets or sets the income of a calendar year above which the tax starts, or <see langword="null"/>: only the
    /// part of the year's income over it is taxed, such as an additional rate on high earners or a second tier of a
    /// pension plan above the first ceiling.
    /// </summary>
    public decimal? WageThreshold { get; set; }

    /// <summary>
    /// Gets or sets an amount of a year's income that is never taxed, taken off each pay in proportion to the pay
    /// periods of a year, such as the basic exemption of a pension plan; or <see langword="null"/>.
    /// </summary>
    public decimal? YearlyExemption { get; set; }

    /// <summary>
    /// Gets or sets the most tax taken in a calendar year, such as a contribution's maximum; or
    /// <see langword="null"/> for no limit.
    /// </summary>
    public decimal? YearlyMaximum { get; set; }

    /// <summary>
    /// Gets or sets the code of another tax of the same pay this rule is a share of, such as the employer's share of a
    /// contribution; the rule's amount is then <see cref="ShareMultiplier"/> times that tax's amount, within
    /// <see cref="YearlyMaximum"/>.
    /// </summary>
    public string ShareOfTaxCode { get; set; }

    /// <summary>
    /// Gets or sets how many times the tax named by <see cref="ShareOfTaxCode"/> this rule takes, such as 1.4.
    /// </summary>
    public decimal? ShareMultiplier { get; set; }

    /// <summary>
    /// Gets or sets the tax tables of a withholding method, by the method's key (such as Single or
    /// MarriedFilingJointly), each a Taxation tax table id.
    /// </summary>
    public Dictionary<string, string> Tables { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the amounts of a withholding method, by the method's key, such as a year's indexed basic amount.
    /// </summary>
    public Dictionary<string, decimal> Parameters { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets a value indicating whether the income of the period is turned into a year's income before the
    /// method is applied, and the result back into a period's tax (how income tax tables are written).
    /// </summary>
    public bool Annualize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the person's election for the jurisdiction applies: exempt withholds
    /// nothing, each allowance lowers a year's income by <see cref="AllowanceAmount"/>, and extra withholding is added.
    /// </summary>
    public bool UsesElections { get; set; }

    /// <summary>
    /// Gets or sets what one allowance lowers a year's income by.
    /// </summary>
    public decimal? AllowanceAmount { get; set; }

    /// <summary>
    /// Gets or sets the flat rate, as a percent, of supplemental income withheld with the flat method, or
    /// <see langword="null"/> to withhold it with regular income.
    /// </summary>
    public decimal? SupplementalRate { get; set; }

    /// <summary>
    /// Gets or sets the rate, as a percent, of the supplemental income a person is paid in a calendar year above
    /// <see cref="SupplementalExcessThreshold"/>, withheld whatever the method and the election, or
    /// <see langword="null"/>.
    /// </summary>
    public decimal? SupplementalExcessRate { get; set; }

    /// <summary>
    /// Gets or sets the supplemental income of a calendar year above which <see cref="SupplementalExcessRate"/> applies.
    /// </summary>
    public decimal? SupplementalExcessThreshold { get; set; }

    /// <summary>
    /// Gets or sets the first day the rule applies (by pay date).
    /// </summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day the rule applies, or <see langword="null"/>.
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rule is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets when it was added.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets whether the rule applies on a pay date.
    /// </summary>
    /// <param name="payDate">The pay date.</param>
    /// <returns><see langword="true"/> when it applies.</returns>
    public bool AppliesOn(DateOnly payDate)
        => Enabled && EffectiveFrom <= payDate && (EffectiveTo is null || EffectiveTo >= payDate);

    /// <summary>
    /// Gets whether the rule applies to a kind of income.
    /// </summary>
    /// <param name="kind">The kind of income.</param>
    /// <returns><see langword="true"/> when it applies.</returns>
    public bool AppliesTo(IncomeKind kind)
        => IncomeKinds is { Count: > 0 } kinds
            ? kinds.Contains(kind)
            : kind == (Category == IncomeTaxCategories.ContractorWithholding ? IncomeKind.Contractor : IncomeKind.Employment);
}
