namespace CrestApps.OrchardCore.Taxation;

/// <summary>
/// Provides shared constant values for the taxation framework.
/// </summary>
public static class TaxationConstants
{
    /// <summary>
    /// Contains the feature identifiers exposed by the taxation module.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The identifier of the core taxation feature.
        /// </summary>
        public const string Taxation = "CrestApps.OrchardCore.Taxation";

        /// <summary>
        /// Works out sales tax with TaxJar for the countries it's set up for.
        /// </summary>
        public const string TaxJar = "CrestApps.OrchardCore.Taxation.TaxJar";

        /// <summary>
        /// The feature with tax jurisdictions, tax tables and the calculation methods, with no Commerce dependency,
        /// shared by Sales Tax and Income Tax.
        /// </summary>
        public const string Tables = "CrestApps.OrchardCore.Taxation.Tables";

        /// <summary>
        /// Taxes on income: income tax withholding at every level and the statutory contributions on earnings, worked
        /// out by income tax rules over the tax tables, with no Commerce or Payroll dependency.
        /// </summary>
        public const string IncomeTax = "CrestApps.OrchardCore.Taxation.IncomeTax";

        /// <summary>
        /// The United States pack of Income Tax: Form W-4 and its withholding method, with the federal rules as dated
        /// recipes.
        /// </summary>
        public const string IncomeTaxUnitedStates = "CrestApps.OrchardCore.Taxation.IncomeTax.UnitedStates";

        /// <summary>
        /// The Canada pack of Income Tax: TD1 claims, the CRA payroll deductions formulas (T4127) for federal,
        /// provincial and territorial tax, Revenu Québec's formulas (TP-1015.F), CPP, QPP, EI and QPIP, with the
        /// figures as dated recipes.
        /// </summary>
        public const string IncomeTaxCanada = "CrestApps.OrchardCore.Taxation.IncomeTax.Canada";

        /// <summary>
        /// Tax returns: the periods each registration files for, worked out from its filing schedule, and a return per
        /// period with the figures the data providers give, its status, payments and amendments.
        /// </summary>
        public const string Returns = "CrestApps.OrchardCore.Taxation.Returns";

        /// <summary>
        /// Electronic filing of tax returns: submissions through filing channels (transports) in file formats, with a
        /// built-in manual channel that hands over a file to file on the agency's site.
        /// </summary>
        public const string EFiling = "CrestApps.OrchardCore.Taxation.EFiling";

        /// <summary>
        /// Tax forms: forms made of fields (their boxes and lines) worked out by formulas, with yearly values and checks,
        /// that a module such as Payroll prepares from its own records.
        /// </summary>
        public const string TaxForms = "CrestApps.OrchardCore.Taxation.TaxForms";
    }

    /// <summary>
    /// The YesSql collection of tax returns and e-file submissions.
    /// </summary>
    public const string TaxReturnsCollectionName = "TaxReturns";

    /// <summary>
    /// The kind of e-file target a tax return is.
    /// </summary>
    public const string TaxReturnEFileTargetType = "TaxReturn";

    /// <summary>
    /// The YesSql collection of tax forms.
    /// </summary>
    public const string TaxFormsCollectionName = "TaxForms";

    /// <summary>
    /// The recipe and deployment step of tax forms.
    /// </summary>
    public const string TaxFormsRecipeStepName = "TaxForms";

    /// <summary>
    /// The YesSql collection of income tax rules.
    /// </summary>
    public const string IncomeTaxCollectionName = "IncomeTax";

    /// <summary>
    /// The recipe and deployment step of income tax rules.
    /// </summary>
    public const string IncomeTaxRulesRecipeStepName = "IncomeTaxRules";

    /// <summary>
    /// Contains the content part names used by the taxation framework.
    /// </summary>
    public static class Parts
    {
        /// <summary>
        /// The technical name of the taxation content part.
        /// </summary>
        public const string TaxationPart = nameof(TaxationPart);
    }

    /// <summary>
    /// The admin settings group identifier for taxation related settings.
    /// </summary>
    public const string SettingsGroupId = "taxation";
}
