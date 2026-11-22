using System.Globalization;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Core.UnitedStates;

/// <summary>
/// The election fields of a state's employee withholding certificate (such as Michigan's MI-W4, Massachusetts' M-4 or
/// Virginia's VA-4), offered for any US state or local jurisdiction (US-*).
/// </summary>
public static class UnitedStatesStateCertificateFields
{
    /// <summary>The number of personal and dependent exemptions claimed (a whole number).</summary>
    public const string Exemptions = "Exemptions";

    /// <summary>The number of exemptions for age or blindness claimed (a whole number).</summary>
    public const string SpecialExemptions = "SpecialExemptions";

    /// <summary>Whether the person files as head of household (true or false).</summary>
    public const string HeadOfHousehold = "HeadOfHousehold";

    /// <summary>
    /// The allowances claimed (a whole number), read by a tax table rule that uses elections, for a state whose
    /// certificate counts allowances rather than exemptions.
    /// </summary>
    public const string Allowances = "Allowances";

    /// <summary>The extra withholding each pay period (an amount with a dot for decimals).</summary>
    public const string ExtraWithholding = "ExtraWithholding";

    /// <summary>Whether the person claims exemption from withholding (true or false).</summary>
    public const string Exempt = "Exempt";

    /// <summary>
    /// Creates the field set, for any US jurisdiction below the country.
    /// </summary>
    /// <returns>The field set.</returns>
    public static ElectionFieldSet Create()
    {
        var set = new ElectionFieldSet
        {
            Jurisdiction = UnitedStatesIncomeTax.Jurisdiction + "-*",
            DisplayName = LocalizationSource.Create("State withholding certificate", typeof(UnitedStatesStateCertificateFields)),
        };

        set.Fields.Add(new ElectionField(Exemptions, LocalizationSource.Create("Exemptions (personal and dependents)", typeof(UnitedStatesStateCertificateFields)), ElectionFieldKind.Integer));
        set.Fields.Add(new ElectionField(SpecialExemptions, LocalizationSource.Create("Exemptions for age or blindness", typeof(UnitedStatesStateCertificateFields)), ElectionFieldKind.Integer));
        set.Fields.Add(new ElectionField(HeadOfHousehold, LocalizationSource.Create("Head of household", typeof(UnitedStatesStateCertificateFields)), ElectionFieldKind.Boolean));
        set.Fields.Add(new ElectionField(Allowances, LocalizationSource.Create("Allowances (states that count allowances)", typeof(UnitedStatesStateCertificateFields)), ElectionFieldKind.Integer));
        set.Fields.Add(new ElectionField(ExtraWithholding, LocalizationSource.Create("Extra withholding each pay period", typeof(UnitedStatesStateCertificateFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(Exempt, LocalizationSource.Create("Exempt from withholding", typeof(UnitedStatesStateCertificateFields)), ElectionFieldKind.Boolean));

        return set;
    }
}

/// <summary>
/// The amounts and the table of a state withholding certificate rule (<see cref="IncomeTaxRule.Parameters"/> and
/// <see cref="IncomeTaxRule.Tables"/>), all a year's.
/// </summary>
public static class UnitedStatesStateCertificateParameters
{
    /// <summary>The table of the brackets of a year's taxable wages (rates in its rows are fractions).</summary>
    public const string Brackets = "Brackets";

    /// <summary>The year's wages at or under which nothing is withheld (Massachusetts' no-tax status).</summary>
    public const string NoTaxThreshold = "NoTaxThreshold";

    /// <summary>The standard deduction everyone gets.</summary>
    public const string StandardDeduction = "StandardDeduction";

    /// <summary>The deduction added for a head of household.</summary>
    public const string HeadOfHouseholdDeduction = "HeadOfHouseholdDeduction";

    /// <summary>The amount of the first exemption claimed.</summary>
    public const string FirstExemption = "FirstExemption";

    /// <summary>The amount of each further exemption claimed.</summary>
    public const string AdditionalExemption = "AdditionalExemption";

    /// <summary>The amount of each exemption for age or blindness.</summary>
    public const string SpecialExemption = "SpecialExemption";

    /// <summary>
    /// The most of the taxes named by <see cref="IncomeTaxRule.DeductedTaxCodes"/> deducted in a year (Massachusetts'
    /// 2,000 of Social Security and Medicare); with none given, all of them.
    /// </summary>
    public const string DeductedTaxesLimit = "DeductedTaxesLimit";
}

/// <summary>
/// State income tax withholding by the annualized method most state withholding guides describe: the period's wages
/// times the periods in a year; nothing when that is at or under the no-tax threshold; less the standard deduction, the
/// head of household deduction, the exemptions (the first, then each further one, then each for age or blindness) and
/// the employee's taxes the rule names (such as Social Security and Medicare) up to a yearly limit; worked out by the
/// brackets and divided by the periods. Exemption and extra withholding are the election's, applied by the provider. A
/// state that withholds this way is a rule with its year's amounts and brackets: data, never code.
/// </summary>
public sealed class UnitedStatesStateCertificateWithholdingMethod : IIncomeTaxWithholdingMethod
{
    private static readonly LocalizationSource _displayName = LocalizationSource.Create<UnitedStatesStateCertificateWithholdingMethod>("United States state withholding certificate (annualized method)");

    private static readonly IReadOnlyList<IncomeTaxWithholdingInput> _tables =
    [
        new(UnitedStatesStateCertificateParameters.Brackets, LocalizationSource.Create<UnitedStatesStateCertificateWithholdingMethod>("Brackets of a year's taxable wages")),
    ];

    private static readonly IReadOnlyList<IncomeTaxWithholdingInput> _parameters =
    [
        new(UnitedStatesStateCertificateParameters.NoTaxThreshold, LocalizationSource.Create<UnitedStatesStateCertificateWithholdingMethod>("Yearly wages at or under which nothing is withheld"), Required: false),
        new(UnitedStatesStateCertificateParameters.StandardDeduction, LocalizationSource.Create<UnitedStatesStateCertificateWithholdingMethod>("Standard deduction"), Required: false),
        new(UnitedStatesStateCertificateParameters.HeadOfHouseholdDeduction, LocalizationSource.Create<UnitedStatesStateCertificateWithholdingMethod>("Head of household deduction"), Required: false),
        new(UnitedStatesStateCertificateParameters.FirstExemption, LocalizationSource.Create<UnitedStatesStateCertificateWithholdingMethod>("First exemption"), Required: false),
        new(UnitedStatesStateCertificateParameters.AdditionalExemption, LocalizationSource.Create<UnitedStatesStateCertificateWithholdingMethod>("Each further exemption"), Required: false),
        new(UnitedStatesStateCertificateParameters.SpecialExemption, LocalizationSource.Create<UnitedStatesStateCertificateWithholdingMethod>("Each exemption for age or blindness"), Required: false),
        new(UnitedStatesStateCertificateParameters.DeductedTaxesLimit, LocalizationSource.Create<UnitedStatesStateCertificateWithholdingMethod>("Most of the deducted taxes a year"), Required: false),
    ];

    /// <inheritdoc />
    public string Name => UnitedStatesIncomeTax.StateCertificateMethod;

    /// <inheritdoc />
    [JsonIgnore]
    public LocalizationSource DisplayName => _displayName;

    /// <inheritdoc />
    public IReadOnlyList<IncomeTaxWithholdingInput> Tables => _tables;

    /// <inheritdoc />
    public IReadOnlyList<IncomeTaxWithholdingInput> Parameters => _parameters;

    /// <inheritdoc />
    public bool UsesOtherTaxes => true;

    /// <inheritdoc />
    public async Task<decimal> CalculateAsync(IncomeTaxWithholdingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var periods = Math.Max(1, context.PeriodsPerYear);
        var annual = context.Income * periods;
        var threshold = context.Parameter(UnitedStatesStateCertificateParameters.NoTaxThreshold);

        if (context.Income <= 0 || (threshold > 0 && annual <= threshold))
        {
            return 0m;
        }

        var exemptions = Count(context, UnitedStatesStateCertificateFields.Exemptions);
        var deductions = context.Parameter(UnitedStatesStateCertificateParameters.StandardDeduction)
            + (context.Value(UnitedStatesStateCertificateFields.HeadOfHousehold) == "true" ? context.Parameter(UnitedStatesStateCertificateParameters.HeadOfHouseholdDeduction) : 0m)
            + (exemptions <= 0 ? 0m : context.Parameter(UnitedStatesStateCertificateParameters.FirstExemption) + ((exemptions - 1) * context.Parameter(UnitedStatesStateCertificateParameters.AdditionalExemption)))
            + (Count(context, UnitedStatesStateCertificateFields.SpecialExemptions) * context.Parameter(UnitedStatesStateCertificateParameters.SpecialExemption))
            + DeductedTaxes(context, periods);
        var taxable = Math.Max(0m, annual - deductions);

        return Math.Max(0m, await context.ComputeTableAsync(UnitedStatesStateCertificateParameters.Brackets, taxable, cancellationToken) / periods);
    }

    // The year's amount of the employee's taxes the rule deducts, from this payment's, up to the yearly limit.
    private static decimal DeductedTaxes(IncomeTaxWithholdingContext context, int periods)
    {
        if (context.Rule.DeductedTaxCodes is not { Count: > 0 } codes)
        {
            return 0m;
        }

        var yearly = Math.Max(0m, codes.Distinct(StringComparer.OrdinalIgnoreCase).Sum(context.OtherTax)) * periods;

        return context.Rule.Parameters?.TryGetValue(UnitedStatesStateCertificateParameters.DeductedTaxesLimit, out var limit) == true
            ? Math.Min(yearly, Math.Max(0m, limit))
            : yearly;
    }

    private static int Count(IncomeTaxWithholdingContext context, string field)
        => int.TryParse(context.Value(field), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? Math.Max(0, count) : 0;
}
