using System.Globalization;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// A way of working out withholding that a jurisdiction's formulas define and that a calculation method over one table
/// can't, such as a tax whose table depends on the filing status a person chose (Form W-4) or whose credits are
/// formula steps (Canada's K1 to K5). An income tax rule names it as its method; the built-in provider hands it the
/// period's income, the person's election, the rule's tables and parameters, and the lines worked out before it (the
/// contributions it gives credits for). A country pack registers one with
/// <c>services.AddIncomeTaxWithholdingMethod&lt;T&gt;()</c>.
/// </summary>
public interface IIncomeTaxWithholdingMethod
{
    /// <summary>
    /// Gets the stored name, kept in <see cref="IncomeTaxRule.Method"/>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets what people see.
    /// </summary>
    LocalizationSource DisplayName { get; }

    /// <summary>
    /// Gets the tax tables a rule must name, by key (<see cref="IncomeTaxRule.Tables"/>); rates in their rows are
    /// fractions and they are worked out as progressive brackets.
    /// </summary>
    IReadOnlyList<IncomeTaxWithholdingInput> Tables { get; }

    /// <summary>
    /// Gets the amounts a rule gives, by key (<see cref="IncomeTaxRule.Parameters"/>).
    /// </summary>
    IReadOnlyList<IncomeTaxWithholdingInput> Parameters { get; }

    /// <summary>
    /// Gets whether the method works out supplemental income itself (such as a bonus method that taxes the year's
    /// income with and without the bonus); otherwise supplemental income is added to the period's income or withheld at
    /// the rule's flat rate.
    /// </summary>
    bool HandlesSupplementalIncome => false;

    /// <summary>
    /// Gets whether the method needs the contributions of the same payment (it runs after the other rules).
    /// </summary>
    bool UsesOtherTaxes => false;

    /// <summary>
    /// Works out the withholding of a period's income, before extra withholding (the provider adds it).
    /// </summary>
    /// <param name="context">The income, the election and the rule.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The withholding of the period, zero or more.</returns>
    Task<decimal> CalculateAsync(IncomeTaxWithholdingContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// A table or an amount a withholding method takes from its rule.
/// </summary>
/// <param name="Key">The stored key.</param>
/// <param name="Label">What the rule's editor shows.</param>
public sealed record IncomeTaxWithholdingInput(string Key, LocalizationSource Label);

/// <summary>
/// What a withholding method works from.
/// </summary>
public sealed class IncomeTaxWithholdingContext
{
    /// <summary>Gets the rule.</summary>
    public required IncomeTaxRule Rule { get; init; }

    /// <summary>Gets the whole request.</summary>
    public required IncomeTaxRequest Request { get; init; }

    /// <summary>Gets the person's election for the rule's jurisdiction, or <see langword="null"/>.</summary>
    public IncomeTaxElection Election { get; init; }

    /// <summary>Gets the regular income of the period to withhold on.</summary>
    public decimal Income { get; init; }

    /// <summary>
    /// Gets the supplemental income of the payment, for a method that <see cref="IIncomeTaxWithholdingMethod.HandlesSupplementalIncome"/>.
    /// </summary>
    public decimal SupplementalIncome { get; init; }

    /// <summary>Gets the pay periods in a year.</summary>
    public int PeriodsPerYear { get; init; }

    /// <summary>
    /// Gets the lines of the same payment worked out before this rule, for a method that
    /// <see cref="IIncomeTaxWithholdingMethod.UsesOtherTaxes"/>.
    /// </summary>
    public IReadOnlyList<IncomeTaxLine> OtherTaxes { get; init; } = [];

    /// <summary>
    /// Gets a function that works out the brackets of one of the rule's tables, by its key, on an amount.
    /// </summary>
    public required Func<string, decimal, CancellationToken, Task<decimal>> ComputeTableAsync { get; init; }

    /// <summary>
    /// Gets a function that gives the lowest rate (a fraction) of one of the rule's tables, by its key: the rate
    /// credits are worked out at.
    /// </summary>
    public Func<string, CancellationToken, Task<decimal>> LowestRateAsync { get; init; }

    /// <summary>
    /// Gets a value of the election, or <see langword="null"/>.
    /// </summary>
    /// <param name="name">The field's name.</param>
    /// <returns>The value.</returns>
    public string Value(string name)
        => Election?.Values is { } values && values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    /// <summary>
    /// Gets an amount of the election (with a dot for decimals), or zero.
    /// </summary>
    /// <param name="name">The field's name.</param>
    /// <returns>The amount.</returns>
    public decimal Amount(string name)
        => AmountOrNull(name) ?? 0m;

    /// <summary>
    /// Gets an amount of the election (with a dot for decimals), or <see langword="null"/> when none was given.
    /// </summary>
    /// <param name="name">The field's name.</param>
    /// <returns>The amount.</returns>
    public decimal? AmountOrNull(string name)
        => decimal.TryParse(Value(name), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount) ? amount : null;

    /// <summary>
    /// Gets a parameter of the rule, or a default.
    /// </summary>
    /// <param name="key">The parameter's key.</param>
    /// <param name="defaultValue">The value when the rule has none.</param>
    /// <returns>The amount.</returns>
    public decimal Parameter(string key, decimal defaultValue = 0m)
        => Rule.Parameters is { } parameters && parameters.TryGetValue(key, out var value) ? value : defaultValue;

    /// <summary>
    /// Gets the amount of another tax of the same payment, by its code, or zero.
    /// </summary>
    /// <param name="taxCode">The tax's code.</param>
    /// <returns>The amount.</returns>
    public decimal OtherTax(string taxCode)
        => OtherTaxes.Where(line => string.Equals(line.TaxCode, taxCode, StringComparison.OrdinalIgnoreCase)).Sum(line => line.Amount);

    /// <summary>
    /// Gets whether the person is in a jurisdiction (home or work).
    /// </summary>
    /// <param name="jurisdiction">The jurisdiction.</param>
    /// <returns>Whether the person is.</returns>
    public bool PersonIn(string jurisdiction)
        => Request.WorkJurisdictions.Concat(Request.HomeJurisdictions).Any(candidate => string.Equals(candidate, jurisdiction, StringComparison.OrdinalIgnoreCase));
}
