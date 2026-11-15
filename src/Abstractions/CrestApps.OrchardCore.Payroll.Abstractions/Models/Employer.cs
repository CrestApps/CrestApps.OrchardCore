using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Payroll.Models;

/// <summary>
/// A legal entity that employs people and pays them: its names, tax id, address, country pack, currency, the bank
/// account it pays from and its tax registrations.
/// </summary>
public sealed class Employer : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the name it is known by.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the legal name printed on statements, checks and forms.
    /// </summary>
    public string LegalName { get; set; }

    /// <summary>
    /// Gets or sets the employer's own tax id, such as its employer identification number. Not a person's.
    /// </summary>
    public string TaxId { get; set; }

    /// <summary>
    /// Gets or sets the address printed on statements and checks.
    /// </summary>
    public Address Address { get; set; }

    /// <summary>
    /// Gets or sets the logo, a path in the media library.
    /// </summary>
    public string LogoPath { get; set; }

    /// <summary>
    /// Gets or sets the country pack the employer's taxes and forms come from, such as US.
    /// </summary>
    public string CountryPack { get; set; }

    /// <summary>
    /// Gets or sets the currency people are paid in.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the bank account pay is sent from, with Banking on.
    /// </summary>
    public string FundingBankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the name printed as the signatory of checks.
    /// </summary>
    public string SignatoryName { get; set; }

    /// <summary>
    /// Gets or sets the signatory's title.
    /// </summary>
    public string SignatoryTitle { get; set; }

    /// <summary>
    /// Gets or sets the payroll contact's name.
    /// </summary>
    public string ContactName { get; set; }

    /// <summary>
    /// Gets or sets the payroll contact's email.
    /// </summary>
    public string ContactEmail { get; set; }

    /// <summary>
    /// Gets or sets the payroll contact's phone.
    /// </summary>
    public string ContactPhone { get; set; }

    /// <summary>
    /// Gets or sets the employer's tax registrations.
    /// </summary>
    public IList<EmployerTaxRegistration> Registrations { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the employer runs payroll.
    /// </summary>
    public EmployerStatus Status { get; set; } = EmployerStatus.Active;

    /// <summary>
    /// Gets or sets when it was added.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets the name to print: the legal name, or the name.
    /// </summary>
    public string PrintedName
        => string.IsNullOrWhiteSpace(LegalName) ? Name : LegalName;
}

/// <summary>
/// Whether an employer runs payroll.
/// </summary>
public enum EmployerStatus
{
    /// <summary>
    /// It runs payroll.
    /// </summary>
    Active,

    /// <summary>
    /// It no longer does; its history stays.
    /// </summary>
    Inactive,
}

/// <summary>
/// An employer's account with a tax agency in a jurisdiction, with its dated rates and how often it deposits.
/// </summary>
public sealed class EmployerTaxRegistration
{
    /// <summary>
    /// Gets or sets the jurisdiction, such as US or US-CA.
    /// </summary>
    public string Jurisdiction { get; set; }

    /// <summary>
    /// Gets or sets the agency, such as the state's employment department.
    /// </summary>
    public string Agency { get; set; }

    /// <summary>
    /// Gets or sets the employer's account id with the agency.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets how often taxes are deposited, such as Monthly or SemiWeekly.
    /// </summary>
    public string DepositFrequency { get; set; }

    /// <summary>
    /// Gets or sets the rates the agency assigned, such as an unemployment rate, each from a day on.
    /// </summary>
    public IList<DatedRate> Rates { get; set; } = [];

    /// <summary>
    /// Gets the rate in effect on a day.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <returns>The rate, or <see langword="null"/>.</returns>
    public decimal? RateOn(DateOnly day)
        => Rates.Where(rate => rate.EffectiveFrom <= day).OrderByDescending(rate => rate.EffectiveFrom).FirstOrDefault()?.Rate;
}

/// <summary>
/// A rate from a day on, as a percent.
/// </summary>
public sealed class DatedRate
{
    /// <summary>
    /// Gets or sets the first day of the rate.
    /// </summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the rate, as a percent such as 2.7.
    /// </summary>
    public decimal Rate { get; set; }
}

/// <summary>
/// Payroll's settings on a team: its ledger code as a cost center and, on a work site, its tax jurisdictions.
/// </summary>
public sealed class PayrollTeamSettings
{
    /// <summary>
    /// Gets or sets the ledger code the team's pay costs go to.
    /// </summary>
    public string LedgerCode { get; set; }

    /// <summary>
    /// Gets or sets the tax jurisdictions of a work site, such as US-CA and US-CA-SF.
    /// </summary>
    public IList<string> Jurisdictions { get; set; } = [];
}

/// <summary>
/// Payroll's site settings.
/// </summary>
public sealed class PayrollSettings
{
    /// <summary>
    /// Gets or sets whether the person who prepared a pay run may approve it. Off by default; on for a one-person
    /// business.
    /// </summary>
    public bool AllowPreparerToApprove { get; set; }

    /// <summary>
    /// Gets or sets whether people may change their own pay rate, pay items, inputs and garnishments through the admin
    /// pages. Off by default.
    /// </summary>
    public bool AllowChangingOwnPay { get; set; }

    /// <summary>
    /// Gets or sets the country pack of a new employer.
    /// </summary>
    public string DefaultCountryPack { get; set; }

    /// <summary>
    /// Gets or sets the currency of a new employer.
    /// </summary>
    public string DefaultCurrency { get; set; }

    /// <summary>
    /// Gets or sets the name of the tax provider that works out taxes, or <see langword="null"/> for the only one
    /// there is.
    /// </summary>
    public string TaxProvider { get; set; }

    /// <summary>
    /// Gets or sets the least net pay a statement may have; deductions taken after tax are cut below it.
    /// </summary>
    public decimal MinimumNetPay { get; set; }

    /// <summary>
    /// Gets or sets the least an hour a tipped person must earn with their tips, or <see langword="null"/> for no tip
    /// credit check (7.25 under the federal law of the United States, more in many states).
    /// </summary>
    public decimal? MinimumHourlyWage { get; set; }

    /// <summary>
    /// Gets or sets the most an hour of tips counts toward the minimum wage (5.12 under the federal law of the United
    /// States: 7.25 less the 2.13 cash wage).
    /// </summary>
    public decimal? MaximumTipCredit { get; set; }
}
