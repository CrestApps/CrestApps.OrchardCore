using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Core.Canada;

/// <summary>
/// The names the Canada Income Tax pack stores: jurisdictions, tax codes, election fields, methods and their parameters.
/// </summary>
public static class CanadaIncomeTax
{
    /// <summary>The federal jurisdiction (the CRA).</summary>
    public const string Jurisdiction = "CA";

    /// <summary>Quebec, whose own agency (Revenu Québec) collects its income tax, QPP and QPIP.</summary>
    public const string Quebec = "CA-QC";

    /// <summary>The federal withholding method (T4127, Chapter 4, Option 1).</summary>
    public const string FederalMethod = "CanadaFederal";

    /// <summary>The provincial and territorial withholding method of the provinces the CRA collects for (T4127).</summary>
    public const string ProvincialMethod = "CanadaProvincial";

    /// <summary>Quebec's withholding method (Revenu Québec, TP-1015.F).</summary>
    public const string QuebecMethod = "QuebecTP1015";

    /// <summary>The tax categories the pack adds.</summary>
    public static class Categories
    {
        /// <summary>
        /// Insurable earnings, the base of Employment Insurance and the Quebec Parental Insurance Plan; remitted with the
        /// other source deductions.
        /// </summary>
        public const string InsurableEarnings = "InsurableEarnings";
    }

    /// <summary>The tax codes the methods read the contributions of the same pay by.</summary>
    public static class TaxCodes
    {
        /// <summary>Federal income tax.</summary>
        public const string FederalIncomeTax = "CA-FIT";

        /// <summary>Canada Pension Plan, base and first additional, the employee's.</summary>
        public const string Cpp = "CA-CPP";

        /// <summary>Canada Pension Plan, second additional (CPP2), the employee's.</summary>
        public const string Cpp2 = "CA-CPP2";

        /// <summary>Employment Insurance outside Quebec, the employee's.</summary>
        public const string EmploymentInsurance = "CA-EI";

        /// <summary>Employment Insurance at Quebec's reduced rate, the employee's.</summary>
        public const string EmploymentInsuranceQuebec = "CA-EI-QC";

        /// <summary>Quebec Pension Plan, base and first additional, the employee's.</summary>
        public const string Qpp = "CA-QC-QPP";

        /// <summary>Quebec Pension Plan, second additional (QPP2), the employee's.</summary>
        public const string Qpp2 = "CA-QC-QPP2";

        /// <summary>Quebec Parental Insurance Plan, the employee's.</summary>
        public const string Qpip = "CA-QC-QPIP";

        /// <summary>Quebec income tax.</summary>
        public const string QuebecIncomeTax = "CA-QC-PIT";
    }

    /// <summary>The election fields of the TD1 forms and of Quebec's TP-1015.3.</summary>
    public static class Fields
    {
        /// <summary>The total claim amount (TD1 line 13, a provincial TD1's total, or TP-1015.3 lines 7 and 9).</summary>
        public const string ClaimAmount = "ClaimAmount";

        /// <summary>Additional tax to deduct each pay (TD1 or TP-1015.3).</summary>
        public const string ExtraWithholding = "ExtraWithholding";

        /// <summary>The annual deduction for living in a prescribed zone (HD).</summary>
        public const string PrescribedZone = "PrescribedZone";

        /// <summary>Other annual deductions a tax services office authorized (F1), or TP-1015.3 line 19 (J).</summary>
        public const string AnnualDeductions = "AnnualDeductions";

        /// <summary>The dependants the Ontario tax reduction counts (under 19, or disabled).</summary>
        public const string Dependants = "Dependants";

        /// <summary>Exempt from withholding (income below the claim, or exempt on TP-1015.3).</summary>
        public const string Exempt = "Exempt";
    }

    /// <summary>The parameters a rule of the Canadian methods gives (amounts; rates as fractions).</summary>
    public static class Parameters
    {
        /// <summary>The basic personal amount, the claim when no form was filed (federal BPAF, provincial TCP, Quebec E1).</summary>
        public const string BasicAmount = "BasicAmount";

        /// <summary>The basic personal amount at and above the reduction's end, when it falls with net income.</summary>
        public const string BasicAmountMinimum = "BasicAmountMinimum";

        /// <summary>The net income the basic personal amount starts falling at.</summary>
        public const string BasicAmountReductionStart = "BasicAmountReductionStart";

        /// <summary>The net income the basic personal amount stops falling at.</summary>
        public const string BasicAmountReductionEnd = "BasicAmountReductionEnd";

        /// <summary>The Canada employment amount (CEA) the K4 or K4P credit is worked out on.</summary>
        public const string CanadaEmploymentAmount = "CanadaEmploymentAmount";

        /// <summary>The CPP contribution rate, base and first additional (0.0595).</summary>
        public const string CppRate = "CppRate";

        /// <summary>The CPP base rate (0.0495).</summary>
        public const string CppBaseRate = "CppBaseRate";

        /// <summary>The yearly maximum of base CPP contributions credits are given for.</summary>
        public const string CppBaseMaximum = "CppBaseMaximum";

        /// <summary>The QPP contribution rate, base and first additional (0.0630).</summary>
        public const string QppRate = "QppRate";

        /// <summary>The QPP base rate (0.0530).</summary>
        public const string QppBaseRate = "QppBaseRate";

        /// <summary>The yearly maximum of base QPP contributions credits are given for.</summary>
        public const string QppBaseMaximum = "QppBaseMaximum";

        /// <summary>The yearly maximum EI premium outside Quebec.</summary>
        public const string EiMaximum = "EiMaximum";

        /// <summary>The yearly maximum EI premium in Quebec.</summary>
        public const string EiQuebecMaximum = "EiQuebecMaximum";

        /// <summary>The yearly maximum QPIP premium.</summary>
        public const string QpipMaximum = "QpipMaximum";

        /// <summary>The share of basic federal tax Quebec's abatement takes off (0.165).</summary>
        public const string QuebecAbatement = "QuebecAbatement";

        /// <summary>Ontario's surtax: the first threshold of basic provincial tax.</summary>
        public const string SurtaxThreshold1 = "SurtaxThreshold1";

        /// <summary>Ontario's surtax: the rate above the first threshold.</summary>
        public const string SurtaxRate1 = "SurtaxRate1";

        /// <summary>Ontario's surtax: the second threshold.</summary>
        public const string SurtaxThreshold2 = "SurtaxThreshold2";

        /// <summary>Ontario's surtax: the rate added above the second threshold.</summary>
        public const string SurtaxRate2 = "SurtaxRate2";

        /// <summary>Ontario's tax reduction: the basic amount (300).</summary>
        public const string DependantTaxReductionBase = "DependantTaxReductionBase";

        /// <summary>Ontario's tax reduction: the amount for each dependant (554).</summary>
        public const string DependantTaxReductionPerDependant = "DependantTaxReductionPerDependant";

        /// <summary>British Columbia's tax reduction: the amount (575, 805 from July 2026).</summary>
        public const string IncomeTaxReductionAmount = "IncomeTaxReductionAmount";

        /// <summary>British Columbia's tax reduction: the income it starts falling at.</summary>
        public const string IncomeTaxReductionThreshold = "IncomeTaxReductionThreshold";

        /// <summary>British Columbia's tax reduction: how fast it falls (0.0356).</summary>
        public const string IncomeTaxReductionRate = "IncomeTaxReductionRate";

        /// <summary>Alberta's supplemental credit (K5P): the credits above which it starts.</summary>
        public const string SupplementalCreditThreshold = "SupplementalCreditThreshold";

        /// <summary>Alberta's supplemental credit (K5P): its rate.</summary>
        public const string SupplementalCreditRate = "SupplementalCreditRate";

        /// <summary>Quebec's deduction for workers: its rate (0.06).</summary>
        public const string WorkerDeductionRate = "WorkerDeductionRate";

        /// <summary>Quebec's deduction for workers: its yearly maximum (1,450).</summary>
        public const string WorkerDeductionMaximum = "WorkerDeductionMaximum";

        /// <summary>Quebec's lump sum rule: the year's income at or below which a lump sum is withheld at a flat rate.</summary>
        public const string LumpSumThreshold = "LumpSumThreshold";

        /// <summary>Quebec's lump sum rule: the flat rate (0.07).</summary>
        public const string LumpSumRate = "LumpSumRate";
    }

    /// <summary>
    /// Registers the Canada pack: the TD1 and TP-1015.3 election fields and the three withholding methods.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddCanadaIncomeTax(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddIncomeTaxCategory(Categories.InsurableEarnings, LocalizationSource.Create("Insurable earnings (EI, QPIP)", typeof(CanadaIncomeTax)));
        services.AddElectionFieldSet(CanadaElectionFields.Federal());
        services.AddElectionFieldSet(CanadaElectionFields.Provincial());
        services.AddElectionFieldSet(CanadaElectionFields.Quebec());
        services.AddIncomeTaxWithholdingMethod<CanadaFederalWithholdingMethod>();
        services.AddIncomeTaxWithholdingMethod<CanadaProvincialWithholdingMethod>();
        services.AddIncomeTaxWithholdingMethod<QuebecWithholdingMethod>();

        return services;
    }
}

/// <summary>
/// The election fields of the federal TD1, the provincial and territorial TD1 forms and Quebec's TP-1015.3.
/// </summary>
public static class CanadaElectionFields
{
    /// <summary>
    /// The federal TD1, for CA.
    /// </summary>
    /// <returns>The field set.</returns>
    public static ElectionFieldSet Federal()
    {
        var set = new ElectionFieldSet
        {
            Jurisdiction = CanadaIncomeTax.Jurisdiction,
            DisplayName = LocalizationSource.Create("TD1 (federal personal tax credits)", typeof(CanadaElectionFields)),
        };

        set.Fields.Add(new ElectionField(CanadaIncomeTax.Fields.ClaimAmount, LocalizationSource.Create("Total claim amount (TD1, line 13); empty for the basic personal amount", typeof(CanadaElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(CanadaIncomeTax.Fields.ExtraWithholding, LocalizationSource.Create("Additional tax to deduct each pay", typeof(CanadaElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(CanadaIncomeTax.Fields.PrescribedZone, LocalizationSource.Create("Deduction for living in a prescribed zone, a year (TD1, HD)", typeof(CanadaElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(CanadaIncomeTax.Fields.AnnualDeductions, LocalizationSource.Create("Other deductions a tax services office authorized, a year (F1)", typeof(CanadaElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(CanadaIncomeTax.Fields.Exempt, LocalizationSource.Create("Exempt: the year's income is below the claim amount", typeof(CanadaElectionFields)), ElectionFieldKind.Boolean));

        return set;
    }

    /// <summary>
    /// A provincial or territorial TD1 (TD1ON, TD1BC...), for any CA- jurisdiction but Quebec.
    /// </summary>
    /// <returns>The field set.</returns>
    public static ElectionFieldSet Provincial()
    {
        var set = new ElectionFieldSet
        {
            Jurisdiction = CanadaIncomeTax.Jurisdiction + "-*",
            DisplayName = LocalizationSource.Create("Provincial or territorial TD1", typeof(CanadaElectionFields)),
        };

        set.Fields.Add(new ElectionField(CanadaIncomeTax.Fields.ClaimAmount, LocalizationSource.Create("Total claim amount (provincial TD1); empty for the basic personal amount", typeof(CanadaElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(CanadaIncomeTax.Fields.Dependants, LocalizationSource.Create("Dependants under 19 or disabled (Ontario tax reduction)", typeof(CanadaElectionFields)), ElectionFieldKind.Integer));

        return set;
    }

    /// <summary>
    /// Quebec's TP-1015.3, for CA-QC.
    /// </summary>
    /// <returns>The field set.</returns>
    public static ElectionFieldSet Quebec()
    {
        var set = new ElectionFieldSet
        {
            Jurisdiction = CanadaIncomeTax.Quebec,
            DisplayName = LocalizationSource.Create("TP-1015.3 (Quebec source deductions return)", typeof(CanadaElectionFields)),
        };

        set.Fields.Add(new ElectionField(CanadaIncomeTax.Fields.ClaimAmount, LocalizationSource.Create("Total claim amount (TP-1015.3, lines 7 and 9); empty for the basic personal amount", typeof(CanadaElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(CanadaIncomeTax.Fields.AnnualDeductions, LocalizationSource.Create("Deductions, a year (TP-1015.3, line 19)", typeof(CanadaElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(CanadaIncomeTax.Fields.ExtraWithholding, LocalizationSource.Create("Additional tax to deduct each pay", typeof(CanadaElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField(CanadaIncomeTax.Fields.Exempt, LocalizationSource.Create("Exempt from Quebec income tax withholding", typeof(CanadaElectionFields)), ElectionFieldKind.Boolean));

        return set;
    }
}
