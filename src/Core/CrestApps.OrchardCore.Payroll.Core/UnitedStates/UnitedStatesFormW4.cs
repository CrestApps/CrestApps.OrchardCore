using CrestApps.OrchardCore.Payroll.Services;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Core.UnitedStates;

/// <summary>
/// The names the United States pack stores: the Form W-4 election fields, the withholding method and its tables.
/// </summary>
public static class UnitedStatesPayroll
{
    /// <summary>The federal jurisdiction.</summary>
    public const string Jurisdiction = "US";

    /// <summary>The withholding method's stored name.</summary>
    public const string FormW4Method = "UnitedStatesFormW4";

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
            Jurisdiction = UnitedStatesPayroll.Jurisdiction,
            DisplayName = LocalizationSource.Create("Form W-4 (federal withholding)", typeof(UnitedStatesElectionFields)),
        };

        set.Fields.Add(new ElectionField(UnitedStatesPayroll.Fields.FormYear, LocalizationSource.Create("Form year", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Choice)
        {
            Choices =
            [
                ("2020", LocalizationSource.Create("2020 or later", typeof(UnitedStatesElectionFields))),
                ("2019", LocalizationSource.Create("2019 or earlier (allowances)", typeof(UnitedStatesElectionFields))),
            ],
        });
        set.Fields.Add(new ElectionField(UnitedStatesPayroll.Fields.FilingStatus, LocalizationSource.Create("Filing status (Step 1c)", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Choice)
        {
            Choices =
            [
                (UnitedStatesPayroll.FilingStatuses.Single, LocalizationSource.Create("Single or married filing separately", typeof(UnitedStatesElectionFields))),
                (UnitedStatesPayroll.FilingStatuses.MarriedFilingJointly, LocalizationSource.Create("Married filing jointly or qualifying surviving spouse", typeof(UnitedStatesElectionFields))),
                (UnitedStatesPayroll.FilingStatuses.HeadOfHousehold, LocalizationSource.Create("Head of household", typeof(UnitedStatesElectionFields))),
            ],
        });
        set.Fields.Add(new ElectionField(UnitedStatesPayroll.Fields.MultipleJobs, LocalizationSource.Create("Multiple jobs or spouse works (Step 2c)", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Boolean));
        set.Fields.Add(new ElectionField(UnitedStatesPayroll.Fields.Dependents, LocalizationSource.Create("Dependents and other credits, a year (Step 3)", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(UnitedStatesPayroll.Fields.OtherIncome, LocalizationSource.Create("Other income, a year (Step 4a)", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(UnitedStatesPayroll.Fields.Deductions, LocalizationSource.Create("Deductions, a year (Step 4b)", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(UnitedStatesPayroll.Fields.ExtraWithholding, LocalizationSource.Create("Extra withholding each pay period (Step 4c)", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(UnitedStatesPayroll.Fields.Allowances, LocalizationSource.Create("Allowances (form from 2019 or earlier)", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Integer));
        set.Fields.Add(new ElectionField(UnitedStatesPayroll.Fields.Exempt, LocalizationSource.Create("Exempt from withholding", typeof(UnitedStatesElectionFields)), ElectionFieldKind.Boolean));

        return set;
    }
}

/// <summary>
/// Federal income tax withholding by the percentage method for automated payroll systems (IRS Publication 15-T,
/// Worksheet 1A). A form from 2020 or later: the period's wages times the periods in a year, plus other income, less
/// deductions and, when the Step 2 box is not checked, the line 1g adjustment; the annual table of the filing status
/// (the Step 2 table when the box is checked); divided by the periods, less the dependents credit for a period. A form
/// from 2019 or earlier: the year's wages less the allowances, on the standard table of single or married filing
/// jointly. A person with no election is withheld as single with no adjustments. Extra withholding, exemption and the
/// supplemental rates are the rule's, applied by the provider.
/// </summary>
public sealed class UnitedStatesFormW4WithholdingMethod : IPayrollWithholdingMethod
{
    private static readonly LocalizationSource _displayName = LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("United States Form W-4 (percentage method)");

    private static readonly IReadOnlyList<PayrollWithholdingInput> _tables =
    [
        new(UnitedStatesPayroll.FilingStatuses.Single, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Single or married filing separately")),
        new(UnitedStatesPayroll.FilingStatuses.MarriedFilingJointly, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Married filing jointly")),
        new(UnitedStatesPayroll.FilingStatuses.HeadOfHousehold, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Head of household")),
        new(UnitedStatesPayroll.FilingStatuses.Single + UnitedStatesPayroll.MultipleJobsSuffix, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Single or married filing separately, Step 2 checked")),
        new(UnitedStatesPayroll.FilingStatuses.MarriedFilingJointly + UnitedStatesPayroll.MultipleJobsSuffix, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Married filing jointly, Step 2 checked")),
        new(UnitedStatesPayroll.FilingStatuses.HeadOfHousehold + UnitedStatesPayroll.MultipleJobsSuffix, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Head of household, Step 2 checked")),
    ];

    private static readonly IReadOnlyList<PayrollWithholdingInput> _parameters =
    [
        new(UnitedStatesPayroll.AdjustmentMarriedFilingJointly, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Line 1g adjustment, married filing jointly")),
        new(UnitedStatesPayroll.Adjustment, LocalizationSource.Create<UnitedStatesFormW4WithholdingMethod>("Line 1g adjustment, other statuses")),
    ];

    /// <inheritdoc />
    public string Name => UnitedStatesPayroll.FormW4Method;

    /// <inheritdoc />
    public LocalizationSource DisplayName => _displayName;

    /// <inheritdoc />
    public IReadOnlyList<PayrollWithholdingInput> Tables => _tables;

    /// <inheritdoc />
    public IReadOnlyList<PayrollWithholdingInput> Parameters => _parameters;

    /// <inheritdoc />
    public async Task<decimal> CalculateAsync(PayrollWithholdingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var periods = Math.Max(1, context.PeriodsPerYear);
        var status = context.Value(UnitedStatesPayroll.Fields.FilingStatus) switch
        {
            UnitedStatesPayroll.FilingStatuses.MarriedFilingJointly => UnitedStatesPayroll.FilingStatuses.MarriedFilingJointly,
            UnitedStatesPayroll.FilingStatuses.HeadOfHousehold => UnitedStatesPayroll.FilingStatuses.HeadOfHousehold,
            _ => UnitedStatesPayroll.FilingStatuses.Single,
        };
        var annual = context.Wages * periods;

        if (context.Value(UnitedStatesPayroll.Fields.FormYear) == "2019")
        {
            // Lines 1j to 1l: the allowances, on the standard table of single or married.
            var allowances = Math.Max(0, context.Amount(UnitedStatesPayroll.Fields.Allowances)) * (context.Rule.AllowanceAmount ?? 0);
            var table = status == UnitedStatesPayroll.FilingStatuses.MarriedFilingJointly ? status : UnitedStatesPayroll.FilingStatuses.Single;

            return Math.Max(0, await context.ComputeTableAsync(table, Math.Max(0, annual - allowances), cancellationToken) / periods);
        }

        var multipleJobs = context.Value(UnitedStatesPayroll.Fields.MultipleJobs) == "true";

        // Lines 1d to 1i.
        annual += Math.Max(0, context.Amount(UnitedStatesPayroll.Fields.OtherIncome)) - Math.Max(0, context.Amount(UnitedStatesPayroll.Fields.Deductions));

        if (!multipleJobs)
        {
            annual -= status == UnitedStatesPayroll.FilingStatuses.MarriedFilingJointly
                ? context.Parameter(UnitedStatesPayroll.AdjustmentMarriedFilingJointly)
                : context.Parameter(UnitedStatesPayroll.Adjustment);
        }

        // Step 2: the tentative withholding for a period; step 3: less the credits for a period.
        var tentative = await context.ComputeTableAsync(multipleJobs ? status + UnitedStatesPayroll.MultipleJobsSuffix : status, Math.Max(0, annual), cancellationToken) / periods;
        var credits = Math.Max(0, context.Amount(UnitedStatesPayroll.Fields.Dependents)) / periods;

        return Math.Max(0, tentative - credits);
    }
}
