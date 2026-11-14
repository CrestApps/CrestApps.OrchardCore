namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// The names Payroll stores and registers.
/// </summary>
public static class PayrollConstants
{
    /// <summary>
    /// The YesSql collection of payroll records.
    /// </summary>
    public const string CollectionName = "Payroll";

    /// <summary>
    /// The recipe and deployment step of pay schedules.
    /// </summary>
    public const string PaySchedulesRecipeStepName = "PaySchedules";

    /// <summary>
    /// The recipe and deployment step of pay codes.
    /// </summary>
    public const string PayCodesRecipeStepName = "PayCodes";

    /// <summary>
    /// The technical name of Payroll's data source of Orchard Core.
    /// </summary>
    public const string DataSourceName = "Payroll";

    /// <summary>
    /// The data set of pay inputs: the hours and amounts other modules hand to payroll.
    /// </summary>
    public const string PayInputsDataSetName = "PayInputs";

    /// <summary>
    /// The Data Pipelines step that records the pay inputs a run read as exported, once the run succeeds.
    /// </summary>
    public const string MarkPayInputsExportedStepName = "MarkPayInputsExported";

    /// <summary>
    /// The prefix of the consumer id a data pipeline run records on the inputs it exported.
    /// </summary>
    public const string PipelineConsumerPrefix = "pipeline:";

    /// <summary>
    /// The party kind of an employer, such as the payer of a check.
    /// </summary>
    public const string EmployerPartyKind = "Employer";

    /// <summary>
    /// The recipe and deployment step of employers.
    /// </summary>
    public const string EmployersRecipeStepName = "PayrollEmployers";

    /// <summary>
    /// The group of Payroll's site settings.
    /// </summary>
    public const string SettingsGroupId = "payroll";

    /// <summary>
    /// The purpose a person's tax id is protected with.
    /// </summary>
    public const string TaxIdPurpose = "CrestApps.OrchardCore.Payroll.TaxId";

    /// <summary>
    /// The recipe and deployment step of pay profiles.
    /// </summary>
    public const string PayProfilesRecipeStepName = "PayrollPayProfiles";

    /// <summary>
    /// The recipe and deployment step of pay items.
    /// </summary>
    public const string PayItemsRecipeStepName = "PayrollPayItems";

    /// <summary>
    /// The name of the recipe step that imports payroll tax rules.
    /// </summary>
    public const string TaxRulesRecipeStepName = "PayrollTaxRules";

    /// <summary>
    /// The data set of pay profiles.
    /// </summary>
    public const string EmployeesDataSetName = "PayrollEmployees";

    /// <summary>
    /// The feature ids of the Payroll module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Pay Setup: pay schedules and pay codes, shared by Workforce's time clock and by Payroll.
        /// </summary>
        public const string PaySetup = "CrestApps.OrchardCore.Payroll.PaySetup";

        /// <summary>
        /// Payroll: employers, pay profiles, pay runs, the engine and statements.
        /// </summary>
        public const string Payroll = "CrestApps.OrchardCore.Payroll";

        /// <summary>
        /// Payroll Tax Tables: the built-in tax provider, payroll tax rules over Taxation's tables.
        /// </summary>
        public const string TaxTables = "CrestApps.OrchardCore.Payroll.TaxTables";
    }
}
