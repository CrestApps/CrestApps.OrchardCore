using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Payroll.Models;

/// <summary>
/// A tax the built-in Payroll Tax Tables provider works out: a jurisdiction's tax on the wages of one tax category,
/// paid by the employee or the employer, by one of Taxation's calculation methods (a rate, a fixed amount, a table or
/// progressive brackets), from a day on. Any jurisdiction of any country can be set up by hand; a country pack ships
/// its own as dated recipes.
/// </summary>
public sealed class PayrollTaxRule : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets what a statement shows, such as "State income tax".
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the tax's code, the same for every dated version of the tax (accumulators and wage bases sum by
    /// it), such as US-CA-SIT.
    /// </summary>
    public string TaxCode { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction, such as US or US-CA.
    /// </summary>
    public string Jurisdiction { get; set; }

    /// <summary>
    /// Gets or sets the tax category whose wages are taxed.
    /// </summary>
    public string Category { get; set; } = PayrollTaxCategories.IncomeTax;

    /// <summary>
    /// Gets or sets who pays.
    /// </summary>
    public PayLinePayer Payer { get; set; }

    /// <summary>
    /// Gets or sets the name of Taxation's calculation method, such as Percentage or Progressive, or of a payroll
    /// withholding method a country pack adds (<c>IPayrollWithholdingMethod</c>), such as UnitedStatesFormW4.
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
    /// Gets or sets the wages taxed in a calendar year at most, or <see langword="null"/> for no limit.
    /// </summary>
    public decimal? WageBase { get; set; }

    /// <summary>
    /// Gets or sets the wages of a calendar year above which the tax starts, or <see langword="null"/>: only the
    /// part of the year's wages over it is taxed, such as an additional rate on high earners.
    /// </summary>
    public decimal? WageThreshold { get; set; }

    /// <summary>
    /// Gets or sets the tax tables of a withholding method, by the method's key (such as Single or
    /// MarriedFilingJointly), each a Taxation tax table id.
    /// </summary>
    public Dictionary<string, string> Tables { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the amounts of a withholding method, by the method's key.
    /// </summary>
    public Dictionary<string, decimal> Parameters { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets a value indicating whether the wages of the period are turned into a year's wages before the
    /// method is applied, and the result back into a period's tax (how income tax tables are written).
    /// </summary>
    public bool Annualize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the person's election for the jurisdiction applies: exempt withholds
    /// nothing, each allowance lowers a year's wages by <see cref="AllowanceAmount"/>, and extra withholding is added.
    /// </summary>
    public bool UsesElections { get; set; }

    /// <summary>
    /// Gets or sets what one allowance lowers a year's wages by.
    /// </summary>
    public decimal? AllowanceAmount { get; set; }

    /// <summary>
    /// Gets or sets the flat rate, as a percent, of supplemental wages withheld with the flat method, or
    /// <see langword="null"/> to withhold them with regular wages.
    /// </summary>
    public decimal? SupplementalRate { get; set; }

    /// <summary>
    /// Gets or sets the rate, as a percent, of the supplemental wages a person is paid in a calendar year above
    /// <see cref="SupplementalExcessThreshold"/>, withheld whatever the method and the election, or
    /// <see langword="null"/>.
    /// </summary>
    public decimal? SupplementalExcessRate { get; set; }

    /// <summary>
    /// Gets or sets the supplemental wages of a calendar year above which <see cref="SupplementalExcessRate"/> applies.
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
}
