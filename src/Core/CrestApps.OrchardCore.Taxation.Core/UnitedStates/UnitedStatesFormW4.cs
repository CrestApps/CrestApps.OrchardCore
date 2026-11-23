using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Core.UnitedStates;

/// <summary>
/// The names the United States Income Tax pack stores: the Form W-4 election fields, the withholding method and its tables.
/// </summary>
public static class UnitedStatesIncomeTax
{
    /// <summary>The federal jurisdiction.</summary>
    public const string Jurisdiction = "US";

    /// <summary>The withholding method's stored name.</summary>
    public const string FormW4Method = "UnitedStatesFormW4";

    /// <summary>
    /// The stored name of the state withholding certificate method and of its kind of rule (the annualized method of
    /// a state's withholding guide).
    /// </summary>
    public const string StateCertificateMethod = "UnitedStatesStateCertificate";

    /// <summary>The election fields of Form W-4.</summary>
    public static class Fields
    {
        /// <summary>The form's year: 2020 (2020 or later) or 2019 (2019 or earlier).</summary>
        public const string FormYear = "FormYear";

        /// <summary>Step 1(c): Single, MarriedFilingJointly or HeadOfHousehold.</summary>
        public const string FilingStatus = "FilingStatus";

        /// <summary>Step 2(c): multiple jobs or a spouse who works.</summary>
        public const string MultipleJobs = "MultipleJobs";

        /// <summary>Step 3: the credit for dependents and others, a year.</summary>
        public const string Dependents = "Dependents";

        /// <summary>Step 4(a): other income, a year.</summary>
        public const string OtherIncome = "OtherIncome";

        /// <summary>Step 4(b): deductions, a year.</summary>
        public const string Deductions = "Deductions";

        /// <summary>Step 4(c): extra withholding each pay period.</summary>
        public const string ExtraWithholding = "ExtraWithholding";

        /// <summary>The allowances of a form from 2019 or earlier.</summary>
        public const string Allowances = "Allowances";

        /// <summary>Exempt from withholding.</summary>
        public const string Exempt = "Exempt";

        /// <summary>
        /// A nonresident alien (true or false): Publication 15-T adds an amount to the year's wages before withholding
        /// (the rule's <see cref="NonresidentAlienAddition"/> parameter).
        /// </summary>
        public const string NonresidentAlien = "NonresidentAlien";
    }

    /// <summary>The filing statuses (the tables' keys).</summary>
    public static class FilingStatuses
    {
        /// <summary>Single or married filing separately (and the 2019 form's "married, but withhold at the higher single rate").</summary>
        public const string Single = "Single";

        /// <summary>Married filing jointly or qualifying surviving spouse (and the 2019 form's "married").</summary>
        public const string MarriedFilingJointly = "MarriedFilingJointly";

        /// <summary>Head of household.</summary>
        public const string HeadOfHousehold = "HeadOfHousehold";
    }

    /// <summary>The suffix of the tables used when the Step 2 box is checked.</summary>
    public const string MultipleJobsSuffix = ".Step2";

    /// <summary>The parameter Worksheet 1A line 1g takes for married filing jointly.</summary>
    public const string AdjustmentMarriedFilingJointly = "AdjustmentMarriedFilingJointly";

    /// <summary>The parameter Worksheet 1A line 1g takes for the other statuses.</summary>
    public const string Adjustment = "Adjustment";

    /// <summary>
    /// The parameter of the amount Publication 15-T adds to a nonresident alien's year of wages (a form from 2020 or
    /// later), from the year's publication.
    /// </summary>
    public const string NonresidentAlienAddition = "NonresidentAlienAddition";
}

/// <summary>
/// The fields of Form W-4, the employee's federal withholding certificate, for the US jurisdiction.
/// </summary>
public static class UnitedStatesElectionFields
{
    /// <summary>
    /// Creates the field set.
    /// </summary>
    /// <returns>The field set.</returns>
    public static ElectionFieldSet Create()
    {
        var set = new ElectionFieldSet
        {
            Jurisdiction = UnitedStatesIncomeTax.Jurisdiction,
            DisplayName = LocalizationSource.Create("Form W-4 (federal withholding)", typeof(UnitedStatesElectionFields)),
        };

        set.Fields.Add(new ElectionField(UnitedStatesIncomeTax.Fields.FormYear, LocalizationSource.Create("Form year", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Choice)
        {
            Choices =
            [
                ("2020", LocalizationSource.Create("2020 or later", typeof(UnitedStatesElectionFields))),
                ("2019", LocalizationSource.Create("2019 or earlier (allowances)", typeof(UnitedStatesElectionFields))),
            ],
        });
        set.Fields.Add(new ElectionField(UnitedStatesIncomeTax.Fields.FilingStatus, LocalizationSource.Create("Filing status (Step 1c)", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Choice)
        {
            Choices =
            [
                (UnitedStatesIncomeTax.FilingStatuses.Single, LocalizationSource.Create("Single or married filing separately", typeof(UnitedStatesElectionFields))),
                (UnitedStatesIncomeTax.FilingStatuses.MarriedFilingJointly, LocalizationSource.Create("Married filing jointly or qualifying surviving spouse", typeof(UnitedStatesElectionFields))),
                (UnitedStatesIncomeTax.FilingStatuses.HeadOfHousehold, LocalizationSource.Create("Head of household", typeof(UnitedStatesElectionFields))),
            ],
        });
        set.Fields.Add(new ElectionField(UnitedStatesIncomeTax.Fields.MultipleJobs, LocalizationSource.Create("Multiple jobs or spouse works (Step 2c)", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Boolean));
        set.Fields.Add(new ElectionField(UnitedStatesIncomeTax.Fields.Dependents, LocalizationSource.Create("Dependents and other credits, a year (Step 3)", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(UnitedStatesIncomeTax.Fields.OtherIncome, LocalizationSource.Create("Other income, a year (Step 4a)", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(UnitedStatesIncomeTax.Fields.Deductions, LocalizationSource.Create("Deductions, a year (Step 4b)", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(UnitedStatesIncomeTax.Fields.ExtraWithholding, LocalizationSource.Create("Extra withholding each pay period (Step 4c)", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(UnitedStatesIncomeTax.Fields.Allowances, LocalizationSource.Create("Allowances (form from 2019 or earlier)", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Integer));
        set.Fields.Add(new ElectionField(UnitedStatesIncomeTax.Fields.Exempt, LocalizationSource.Create("Exempt from withholding", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Boolean));
        set.Fields.Add(new ElectionField(UnitedStatesIncomeTax.Fields.NonresidentAlien, LocalizationSource.Create("Nonresident alien", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Boolean));

        return set;
    }
}

/// <summary>
/// Federal income tax withholding by the percentage method for automated payroll systems (IRS Publication 15-T,
/// Worksheet 1A). A form from 2020 or later: the period's wages times the periods in a year, plus other income, less
/// deductions and, when the Step 2 box is not checked, the line 1g adjustment; the annual table of the filing status
/// (the Step 2 table when the box is checked); divided by the periods, less the dependents credit for a period. A form
/// from 2019 or earlier: the year's wages less the allowances, on the standard table of single or married filing
/// jointly. A nonresident alien's year of wages (a form from 2020 or later) has the rule's nonresident alien amount
/// added. A person with no election is withheld as single with no adjustments. Extra withholding, exemption and the
/// supplemental rates are the rule's, applied by the provider.
/// </summary>
public sealed class UnitedStatesFormW4WithholdingMethod : IIncomeTaxWithholdingMethod
{
    private static readonly LocalizationSource _displayName = LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("United States Form W-4 (percentage method)");

    private static readonly IReadOnlyList<IncomeTaxWithholdingInput> _tables =
    [
        new(UnitedStatesIncomeTax.FilingStatuses.Single, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Single or married filing separately")),
        new(UnitedStatesIncomeTax.FilingStatuses.MarriedFilingJointly, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Married filing jointly")),
        new(UnitedStatesIncomeTax.FilingStatuses.HeadOfHousehold, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Head of household")),
        new(UnitedStatesIncomeTax.FilingStatuses.Single + UnitedStatesIncomeTax.MultipleJobsSuffix, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Single or married filing separately, Step 2 checked")),
        new(UnitedStatesIncomeTax.FilingStatuses.MarriedFilingJointly + UnitedStatesIncomeTax.MultipleJobsSuffix, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Married filing jointly, Step 2 checked")),
        new(UnitedStatesIncomeTax.FilingStatuses.HeadOfHousehold + UnitedStatesIncomeTax.MultipleJobsSuffix, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Head of household, Step 2 checked")),
    ];

    private static readonly IReadOnlyList<IncomeTaxWithholdingInput> _parameters =
    [
        new(UnitedStatesIncomeTax.AdjustmentMarriedFilingJointly, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Line 1g adjustment, married filing jointly")),
        new(UnitedStatesIncomeTax.Adjustment, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Line 1g adjustment, other statuses")),
        new(UnitedStatesIncomeTax.NonresidentAlienAddition, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Amount added to a nonresident alien's year of wages"), Required: false),
    ];

    /// <inheritdoc />
    public string Name => UnitedStatesIncomeTax.FormW4Method;

    /// <inheritdoc />
    [JsonIgnore]
    public LocalizationSource DisplayName => _displayName;

    /// <inheritdoc />
    public IReadOnlyList<IncomeTaxWithholdingInput> Tables => _tables;

    /// <inheritdoc />
    public IReadOnlyList<IncomeTaxWithholdingInput> Parameters => _parameters;

    /// <inheritdoc />
    public async Task<decimal> CalculateAsync(IncomeTaxWithholdingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var periods = Math.Max(1, context.PeriodsPerYear);
        var status = context.Value(UnitedStatesIncomeTax.Fields.FilingStatus) switch
        {
            UnitedStatesIncomeTax.FilingStatuses.MarriedFilingJointly => UnitedStatesIncomeTax.FilingStatuses.MarriedFilingJointly,
            UnitedStatesIncomeTax.FilingStatuses.HeadOfHousehold => UnitedStatesIncomeTax.FilingStatuses.HeadOfHousehold,
            _ => UnitedStatesIncomeTax.FilingStatuses.Single,
        };
        var annual = context.Income * periods;

        if (context.Value(UnitedStatesIncomeTax.Fields.FormYear) == "2019")
        {
            // Lines 1j to 1l: the allowances, on the standard table of single or married.
            var allowances = Math.Max(0, context.Amount(UnitedStatesIncomeTax.Fields.Allowances)) * (context.Rule.AllowanceAmount ?? 0);
            var table = status == UnitedStatesIncomeTax.FilingStatuses.MarriedFilingJointly ? status : UnitedStatesIncomeTax.FilingStatuses.Single;

            return Math.Max(0, await context.ComputeTableAsync(table, Math.Max(0, annual - allowances), cancellationToken) / periods);
        }

        var multipleJobs = context.Value(UnitedStatesIncomeTax.Fields.MultipleJobs) == "true";

        // Lines 1d to 1i, with the amount added to a nonresident alien's wages (Publication 15-T, nonresident aliens).
        annual += Math.Max(0, context.Amount(UnitedStatesIncomeTax.Fields.OtherIncome)) - Math.Max(0, context.Amount(UnitedStatesIncomeTax.Fields.Deductions));

        if (context.Value(UnitedStatesIncomeTax.Fields.NonresidentAlien) == "true")
        {
            annual += Math.Max(0, context.Parameter(UnitedStatesIncomeTax.NonresidentAlienAddition));
        }

        if (!multipleJobs)
        {
            annual -= status == UnitedStatesIncomeTax.FilingStatuses.MarriedFilingJointly
                ? context.Parameter(UnitedStatesIncomeTax.AdjustmentMarriedFilingJointly)
                : context.Parameter(UnitedStatesIncomeTax.Adjustment);
        }

        // Step 2: the tentative withholding for a period; step 3: less the credits for a period.
        var tentative = await context.ComputeTableAsync(multipleJobs ? status + UnitedStatesIncomeTax.MultipleJobsSuffix : status, Math.Max(0, annual), cancellationToken) / periods;
        var credits = Math.Max(0, context.Amount(UnitedStatesIncomeTax.Fields.Dependents)) / periods;

        return Math.Max(0, tentative - credits);
    }
}
