namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// The sources of income tax rule Taxation adds, and how a rule saved before rules had a source gets one.
/// </summary>
public static class IncomeTaxRuleSources
{
    /// <summary>
    /// A rate on the income, within a yearly wage base, above a threshold, after a yearly exemption or up to a yearly
    /// maximum, or at the payer's own rate (Social Security, Medicare, unemployment insurance, CPP, EI).
    /// </summary>
    public const string FlatRate = "FlatRate";

    /// <summary>
    /// A multiple of another tax of the same pay, such as the employer's share of a contribution.
    /// </summary>
    public const string ShareOfTax = "ShareOfTax";

    /// <summary>
    /// A tax table or progressive brackets, optionally on a year's income with the person's election (a state's
    /// income tax).
    /// </summary>
    public const string TaxTable = "TaxTable";

    /// <summary>
    /// The same amount each pay.
    /// </summary>
    public const string FixedAmount = "FixedAmount";

    /// <summary>
    /// Gets the source of a rule saved or imported without one, from what it holds: a share of another tax, the rate
    /// or fixed amount methods, a withholding method's name (which is its source's name), or a table method.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="isWithholdingMethod">Whether a method name is a withholding method's.</param>
    /// <returns>The source's name.</returns>
    public static string Infer(IncomeTaxRule rule, Func<string, bool> isWithholdingMethod)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (!string.IsNullOrEmpty(rule.ShareOfTaxCode))
        {
            return ShareOfTax;
        }

        if (string.IsNullOrEmpty(rule.Method) || string.Equals(rule.Method, TaxCalculationMethodNames.Percentage, StringComparison.OrdinalIgnoreCase))
        {
            return FlatRate;
        }

        if (string.Equals(rule.Method, TaxCalculationMethodNames.FixedAmount, StringComparison.OrdinalIgnoreCase))
        {
            return FixedAmount;
        }

        return isWithholdingMethod?.Invoke(rule.Method) == true ? rule.Method : TaxTable;
    }
}
