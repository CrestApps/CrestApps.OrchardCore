using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Works out the taxes on a payment of income. Stateless: everything it needs is in the request, so a commercial tax
/// engine, a full-service provider and the built-in rules all fit behind it.
/// </summary>
public interface IIncomeTaxProvider
{
    /// <summary>
    /// Gets the stored name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets what people see.
    /// </summary>
    [JsonIgnore]
    LocalizationSource DisplayName { get; }

    /// <summary>
    /// Works out the taxes.
    /// </summary>
    /// <param name="request">The payment.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The tax lines.</returns>
    Task<IncomeTaxResult> CalculateAsync(IncomeTaxRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the jurisdictions whose election (the values of a withholding form, such as Form W-4) the provider reads on a
    /// day, so a payroll asks people only for the elections a tax in effect takes: a city with no income tax, or one
    /// whose tax reads the state's certificate, needs none of its own.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>
    /// The jurisdictions, compared without regard to case, or <see langword="null"/> when the provider can't tell, and an
    /// election is then asked for every jurisdiction a person is registered or works in. The default is
    /// <see langword="null"/>.
    /// </returns>
    Task<IReadOnlySet<string>> GetElectionJurisdictionsAsync(DateOnly date, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlySet<string>>(null);
}

/// <summary>
/// What a tax provider needs to work out the taxes on a payment of income: who pays and who earns it, where, the
/// income of each tax category and what was taxed earlier in the year. It names no module's records, so payroll and
/// any other module that pays income can use it.
/// </summary>
public sealed class IncomeTaxRequest
{
    /// <summary>Gets or sets the payer (an employer), for providers that keep their own records.</summary>
    public string PayerId { get; set; }

    /// <summary>Gets or sets the jurisdictions the payer is registered in for taxes on income.</summary>
    public IList<string> RegisteredJurisdictions { get; set; } = [];

    /// <summary>
    /// Gets or sets the payer's registrations: its account with each jurisdiction's agency and the rate the agency
    /// assigned it on the pay date (an unemployment insurance rate, Quebec's health services fund rate). A jurisdiction
    /// here counts as registered too.
    /// </summary>
    public IList<IncomeTaxRegistration> Registrations { get; set; } = [];

    /// <summary>Gets or sets the earner, for providers that keep their own records.</summary>
    public string EarnerId { get; set; }

    /// <summary>Gets or sets the kind of income.</summary>
    public IncomeKind IncomeKind { get; set; }

    /// <summary>
    /// Gets or sets whether a contractor's payments are withheld on, such as when no tax id is on file.
    /// </summary>
    public bool ContractorWithholding { get; set; }

    /// <summary>Gets or sets the jurisdictions of the earner's home.</summary>
    public IList<string> HomeJurisdictions { get; set; } = [];

    /// <summary>Gets or sets the jurisdictions where the work is done (the province of employment in Canada).</summary>
    public IList<string> WorkJurisdictions { get; set; } = [];

    /// <summary>Gets or sets the elections in effect on the pay date, one per jurisdiction.</summary>
    public IList<IncomeTaxElection> Elections { get; set; } = [];

    /// <summary>Gets or sets the pay date.</summary>
    public DateOnly PayDate { get; set; }

    /// <summary>Gets or sets the pay periods in a year.</summary>
    public int PeriodsPerYear { get; set; }

    /// <summary>Gets or sets the regular income of each tax category.</summary>
    public IDictionary<string, decimal> Income { get; set; } = new Dictionary<string, decimal>(StringComparer.Ordinal);

    /// <summary>Gets or sets the supplemental income of each tax category (part of none of <see cref="Income"/>).</summary>
    public IDictionary<string, decimal> SupplementalIncome { get; set; } = new Dictionary<string, decimal>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the earner's regular income of each tax category for one pay period (from their latest regular
    /// payment or their salary), sent with a payment that has no regular income of its own, such as a bonus paid
    /// alone: withholding methods that add supplemental income to a year of regular pay (Canada's bonus method, an
    /// aggregate method) add it to this pay instead of to nothing.
    /// </summary>
    public IDictionary<string, decimal> RegularIncomePerPeriod { get; set; } = new Dictionary<string, decimal>(StringComparer.Ordinal);

    /// <summary>Gets or sets how supplemental income is withheld on.</summary>
    public SupplementalWithholdingMethod SupplementalMethod { get; set; }

    /// <summary>
    /// Gets or sets the income of each tax category paid this calendar year before this payment (regular and
    /// supplemental).
    /// </summary>
    public IDictionary<string, decimal> YearIncome { get; set; } = new Dictionary<string, decimal>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the supplemental income of each tax category paid this calendar year before this payment.
    /// </summary>
    public IDictionary<string, decimal> YearSupplementalIncome { get; set; } = new Dictionary<string, decimal>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets what each tax (by its code) was worked out on and came to earlier this year and this period, for
    /// wage bases, yearly maximums and a second payment in a period.
    /// </summary>
    public IDictionary<string, IncomeTaxToDate> TaxesToDate { get; set; } = new Dictionary<string, IncomeTaxToDate>(StringComparer.Ordinal);

    /// <summary>Gets or sets the currency of the amounts.</summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets what a tax was worked out on and came to before this payment, or zeros.
    /// </summary>
    /// <param name="taxCode">The tax's code.</param>
    /// <returns>The totals.</returns>
    public IncomeTaxToDate ToDate(string taxCode)
        => taxCode is not null && TaxesToDate.TryGetValue(taxCode, out var totals) && totals is not null ? totals : new IncomeTaxToDate();
}

/// <summary>
/// A payer's registration with a jurisdiction's agency for taxes on income.
/// </summary>
public sealed class IncomeTaxRegistration
{
    /// <summary>Gets or sets the jurisdiction, such as US-CA or CA-QC.</summary>
    public string Jurisdiction { get; set; }

    /// <summary>Gets or sets the payer's account id with the agency.</summary>
    public string AccountId { get; set; }

    /// <summary>Gets or sets the rate the agency assigned the payer, in effect on the pay date, as a percent; or <see langword="null"/>.</summary>
    public decimal? Rate { get; set; }

    /// <summary>
    /// Gets or sets the tax categories the registration covers, such as Unemployment, when the payer has several
    /// registrations in one jurisdiction (income tax withholding and unemployment insurance with a state); empty for any.
    /// A rule that takes the payer's own rate reads the registration of its category first.
    /// </summary>
    public IList<string> Categories { get; set; } = [];
}

/// <summary>
/// A person's election for a jurisdiction (the values of its withholding form, such as Form W-4 or TD1).
/// </summary>
public sealed class IncomeTaxElection
{
    /// <summary>Gets or sets the jurisdiction.</summary>
    public string Jurisdiction { get; set; }

    /// <summary>Gets or sets the values, by field name.</summary>
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets a value, or <see langword="null"/>.
    /// </summary>
    /// <param name="name">The field's name.</param>
    /// <returns>The value.</returns>
    public string Value(string name)
        => Values is not null && Values.TryGetValue(name, out var value) ? value : null;
}

/// <summary>
/// What one tax was worked out on and came to before a payment.
/// </summary>
public sealed class IncomeTaxToDate
{
    /// <summary>Gets or sets the income taxed earlier this calendar year.</summary>
    public decimal YearTaxableIncome { get; set; }

    /// <summary>Gets or sets the tax earlier this calendar year.</summary>
    public decimal YearTax { get; set; }

    /// <summary>Gets or sets the income taxed by earlier payments of this pay period.</summary>
    public decimal PeriodTaxableIncome { get; set; }

    /// <summary>Gets or sets the part of <see cref="PeriodTaxableIncome"/> withheld on at a flat supplemental rate.</summary>
    public decimal PeriodFlatIncome { get; set; }
}

/// <summary>
/// The taxes on a payment.
/// </summary>
public sealed class IncomeTaxResult
{
    /// <summary>Gets the tax lines.</summary>
    public IList<IncomeTaxLine> Lines { get; } = [];

    /// <summary>Gets the warnings, such as a rule whose table is missing.</summary>
    public IList<string> Warnings { get; } = [];
}

/// <summary>
/// A tax on a payment.
/// </summary>
public sealed class IncomeTaxLine
{
    /// <summary>Gets or sets the tax's code, stable across payments (year-to-date totals sum by it).</summary>
    public string TaxCode { get; set; }

    /// <summary>Gets or sets what the statement shows.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the jurisdiction.</summary>
    public string Jurisdiction { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction whose agency the tax is remitted to, such as CA for a province's tax the CRA
    /// collects; the same as <see cref="Jurisdiction"/> when that agency collects its own.
    /// </summary>
    public string CollectedBy { get; set; }

    /// <summary>Gets or sets the tax category the income was taken from.</summary>
    public string Category { get; set; }

    /// <summary>Gets or sets who pays.</summary>
    public IncomeTaxPayer Payer { get; set; }

    /// <summary>Gets or sets the income the tax was worked out on.</summary>
    public decimal TaxableIncome { get; set; }

    /// <summary>Gets or sets the part of the taxable income withheld on at a flat supplemental rate.</summary>
    public decimal FlatIncome { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    public decimal Amount { get; set; }
}
