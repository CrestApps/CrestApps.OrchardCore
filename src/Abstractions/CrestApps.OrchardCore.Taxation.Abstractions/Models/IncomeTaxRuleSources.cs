namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// The sources of income tax rule Taxation adds, the fields each owns, and how a rule saved before rules had a source
/// gets one.
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
    /// The fields of a flat rate rule.
    /// </summary>
    public static readonly IReadOnlyList<string> FlatRateFields =
    [
        nameof(IncomeTaxRule.Rate),
        nameof(IncomeTaxRule.RateFromRegistration),
        nameof(IncomeTaxRule.WageBase),
        nameof(IncomeTaxRule.WageThreshold),
        nameof(IncomeTaxRule.YearlyExemption),
        nameof(IncomeTaxRule.YearlyMaximum),
        nameof(IncomeTaxRule.ExemptionAllowance),
    ];

    /// <summary>
    /// The fields of a share of another tax.
    /// </summary>
    public static readonly IReadOnlyList<string> ShareOfTaxFields =
    [
        nameof(IncomeTaxRule.ShareOfTaxCode),
        nameof(IncomeTaxRule.ShareMultiplier),
        nameof(IncomeTaxRule.YearlyMaximum),
    ];

    /// <summary>
    /// The fields of a tax table rule.
    /// </summary>
    public static readonly IReadOnlyList<string> TaxTableFields =
    [
        nameof(IncomeTaxRule.TaxTableId),
        nameof(IncomeTaxRule.WageBase),
        nameof(IncomeTaxRule.Annualize),
        nameof(IncomeTaxRule.UsesElections),
        nameof(IncomeTaxRule.AllowanceAmount),
        nameof(IncomeTaxRule.SupplementalRate),
        nameof(IncomeTaxRule.SupplementalExcessRate),
        nameof(IncomeTaxRule.SupplementalExcessThreshold),
    ];

    /// <summary>
    /// The fields of a fixed amount rule.
    /// </summary>
    public static readonly IReadOnlyList<string> FixedAmountFields =
    [
        nameof(IncomeTaxRule.FixedAmount),
        nameof(IncomeTaxRule.YearlyMaximum),
    ];

    /// <summary>
    /// The fields of a rule worked out by a country pack's withholding method, unless its source says otherwise.
    /// </summary>
    public static readonly IReadOnlyList<string> WithholdingFields =
    [
        nameof(IncomeTaxRule.Tables),
        nameof(IncomeTaxRule.Parameters),
        nameof(IncomeTaxRule.UsesElections),
        nameof(IncomeTaxRule.AllowanceAmount),
        nameof(IncomeTaxRule.SupplementalRate),
        nameof(IncomeTaxRule.SupplementalExcessRate),
        nameof(IncomeTaxRule.SupplementalExcessThreshold),
    ];

    private static readonly string[] _tableMethods = [TaxCalculationMethodNames.TaxTable, TaxCalculationMethodNames.Progressive, TaxCalculationMethodNames.Threshold];

    /// <summary>
    /// Gets the source of a rule saved or imported without one, from what it holds: a share of another tax, the rate
    /// or fixed amount methods, Taxation's table methods, and otherwise the method's own name, which is the source of
    /// a country pack's withholding method (whether its feature is on or not, so a rule of a pack that is off is
    /// never turned into another kind).
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The source's name.</returns>
    public static string Infer(IncomeTaxRule rule)
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

        return _tableMethods.Contains(rule.Method, StringComparer.OrdinalIgnoreCase) ? TaxTable : rule.Method;
    }

    /// <summary>
    /// Returns the amount fields of a rule that hold a value but aren't among the fields its source owns: what saving
    /// the rule under its source drops.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="owned">The fields the source owns.</param>
    /// <returns>The field names.</returns>
    public static IReadOnlyList<string> ValuesNotOwned(IncomeTaxRule rule, IReadOnlyCollection<string> owned)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(owned);

        return [.. Amounts(rule).Where(field => field.HasValue && !owned.Contains(field.Name)).Select(field => field.Name)];
    }

    /// <summary>
    /// Clears the amount fields a source doesn't own, so a rule never carries a value its kind doesn't show and that
    /// the calculation would still read.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="owned">The fields the source owns.</param>
    /// <param name="keep">Fields to leave alone even when not owned, so the handler can refuse them instead.</param>
    public static void ClearNotOwned(IncomeTaxRule rule, IReadOnlyCollection<string> owned, params string[] keep)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(owned);

        foreach (var field in Amounts(rule).Where(field => !owned.Contains(field.Name) && !keep.Contains(field.Name)))
        {
            field.Clear();
        }
    }

    private static IEnumerable<(string Name, bool HasValue, Action Clear)> Amounts(IncomeTaxRule rule)
    {
        yield return (nameof(IncomeTaxRule.Rate), rule.Rate is not null, () => rule.Rate = null);
        yield return (nameof(IncomeTaxRule.RateFromRegistration), rule.RateFromRegistration, () => rule.RateFromRegistration = false);
        yield return (nameof(IncomeTaxRule.FixedAmount), rule.FixedAmount is not null, () => rule.FixedAmount = null);
        yield return (nameof(IncomeTaxRule.TaxTableId), !string.IsNullOrEmpty(rule.TaxTableId), () => rule.TaxTableId = null);
        yield return (nameof(IncomeTaxRule.WageBase), rule.WageBase is not null, () => rule.WageBase = null);
        yield return (nameof(IncomeTaxRule.WageThreshold), rule.WageThreshold is not null, () => rule.WageThreshold = null);
        yield return (nameof(IncomeTaxRule.YearlyExemption), rule.YearlyExemption is not null, () => rule.YearlyExemption = null);
        yield return (nameof(IncomeTaxRule.YearlyMaximum), rule.YearlyMaximum is not null, () => rule.YearlyMaximum = null);
        yield return (nameof(IncomeTaxRule.ShareOfTaxCode), !string.IsNullOrEmpty(rule.ShareOfTaxCode), () => rule.ShareOfTaxCode = null);
        yield return (nameof(IncomeTaxRule.ShareMultiplier), rule.ShareMultiplier is not null, () => rule.ShareMultiplier = null);
        yield return (nameof(IncomeTaxRule.Annualize), rule.Annualize, () => rule.Annualize = false);
        yield return (nameof(IncomeTaxRule.UsesElections), rule.UsesElections, () => rule.UsesElections = false);
        yield return (nameof(IncomeTaxRule.AllowanceAmount), rule.AllowanceAmount is not null, () => rule.AllowanceAmount = null);
        yield return (nameof(IncomeTaxRule.ExemptionAllowance), rule.ExemptionAllowance is not null, () => rule.ExemptionAllowance = null);
        yield return (nameof(IncomeTaxRule.DeductedTaxCodes), rule.DeductedTaxCodes is { Count: > 0 }, () => rule.DeductedTaxCodes = []);
        yield return (nameof(IncomeTaxRule.SupplementalRate), rule.SupplementalRate is not null, () => rule.SupplementalRate = null);
        yield return (nameof(IncomeTaxRule.SupplementalExcessRate), rule.SupplementalExcessRate is not null, () => rule.SupplementalExcessRate = null);
        yield return (nameof(IncomeTaxRule.SupplementalExcessThreshold), rule.SupplementalExcessThreshold is not null, () => rule.SupplementalExcessThreshold = null);
        yield return (nameof(IncomeTaxRule.Tables), rule.Tables is { Count: > 0 }, () => rule.Tables = new Dictionary<string, string>(StringComparer.Ordinal));
        yield return (nameof(IncomeTaxRule.Parameters), rule.Parameters is { Count: > 0 }, () => rule.Parameters = new Dictionary<string, decimal>(StringComparer.Ordinal));
    }
}
