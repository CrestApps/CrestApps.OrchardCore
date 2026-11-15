using CrestApps.OrchardCore.Payroll.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// A way of working out withholding that a jurisdiction's form defines and that a calculation method over one table
/// can't, such as an income tax whose table depends on the filing status a person chose. A payroll tax rule names it
/// as its method; the Payroll Tax Tables provider hands it the period's wages, the person's election and the rule's
/// tables and parameters. A country pack registers one with <c>services.AddPayrollWithholdingMethod&lt;T&gt;()</c>.
/// </summary>
public interface IPayrollWithholdingMethod
{
    /// <summary>
    /// Gets the stored name, kept in <see cref="PayrollTaxRule.Method"/>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets what people see.
    /// </summary>
    LocalizationSource DisplayName { get; }

    /// <summary>
    /// Gets the tax tables a rule must name, by key (<see cref="PayrollTaxRule.Tables"/>); rates in their rows are
    /// fractions and they are worked out as progressive brackets.
    /// </summary>
    IReadOnlyList<PayrollWithholdingInput> Tables { get; }

    /// <summary>
    /// Gets the amounts a rule gives, by key (<see cref="PayrollTaxRule.Parameters"/>).
    /// </summary>
    IReadOnlyList<PayrollWithholdingInput> Parameters { get; }

    /// <summary>
    /// Works out the withholding of a period's wages, before extra withholding (the provider adds it).
    /// </summary>
    /// <param name="context">The wages, the election and the rule.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The withholding of the period, zero or more.</returns>
    Task<decimal> CalculateAsync(PayrollWithholdingContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// A table or an amount a withholding method takes from its rule.
/// </summary>
/// <param name="Key">The stored key.</param>
/// <param name="Label">What the rule's editor shows.</param>
public sealed record PayrollWithholdingInput(string Key, LocalizationSource Label);

/// <summary>
/// What a withholding method works from.
/// </summary>
public sealed class PayrollWithholdingContext
{
    /// <summary>Gets the rule.</summary>
    public required PayrollTaxRule Rule { get; init; }

    /// <summary>Gets the whole request.</summary>
    public required PayrollTaxRequest Request { get; init; }

    /// <summary>Gets the person's election for the rule's jurisdiction, or <see langword="null"/>.</summary>
    public TaxElection Election { get; init; }

    /// <summary>Gets the wages of the period to withhold on.</summary>
    public decimal Wages { get; init; }

    /// <summary>Gets the pay periods in a year.</summary>
    public int PeriodsPerYear { get; init; }

    /// <summary>
    /// Gets a function that works out the brackets of one of the rule's tables, by its key, on an amount.
    /// </summary>
    public required Func<string, decimal, CancellationToken, Task<decimal>> ComputeTableAsync { get; init; }

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
        => decimal.TryParse(Value(name), System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var amount) ? amount : 0m;

    /// <summary>
    /// Gets a parameter of the rule, or a default.
    /// </summary>
    /// <param name="key">The parameter's key.</param>
    /// <param name="defaultValue">The value when the rule has none.</param>
    /// <returns>The amount.</returns>
    public decimal Parameter(string key, decimal defaultValue = 0m)
        => Rule.Parameters is { } parameters && parameters.TryGetValue(key, out var value) ? value : defaultValue;
}
