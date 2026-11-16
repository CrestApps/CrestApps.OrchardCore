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
    /// The prefix of the consumer id a processed pay run takes its source inputs under, followed by the run's id.
    /// </summary>
    public const string PayRunConsumerPrefix = "payrun:";

    /// <summary>
    /// The party kind of an employer, such as the payer of a check.
    /// </summary>
    public const string EmployerPartyKind = "Employer";

    /// <summary>
    /// The recipe and deployment step of employers.
    /// </summary>
    public const string EmployersRecipeStepName = "PayrollEmployers";

    /// <summary>
    /// The opt-in recipe and deployment step of final pay statements, imported as history.
    /// </summary>
    public const string PayHistoryRecipeStepName = "PayrollPayHistory";

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
    /// The name of the recipe and deployment step of garnishment orders.
    /// </summary>
    public const string GarnishmentsRecipeStepName = "PayrollGarnishments";

    /// <summary>
    /// The name of the recipe and deployment step of tax deposits and filings.
    /// </summary>
    public const string FilingsRecipeStepName = "PayrollFilings";

    /// <summary>
    /// The name of the recipe step of tax deposits.
    /// </summary>
    public const string TaxDepositsRecipeStepName = "PayrollTaxDeposits";

    /// <summary>
    /// The notification type telling a person a tax form of theirs is ready, such as a wage statement.
    /// </summary>
    public const string TaxFormReadyNotificationType = "TaxFormReady";

    /// <summary>
    /// The name of the pipeline step that saves rows as the inputs of a pay run.
    /// </summary>
    public const string SavePayInputsStepName = "SavePayInputs";

    /// <summary>
    /// The name of the Approvals policy pay runs are approved by.
    /// </summary>
    public const string PayRunApprovalPolicy = "PayRunApproval";

    /// <summary>
    /// The kind of approval subject, and of source, a pay run is.
    /// </summary>
    public const string PayRunSubjectKind = "PayRun";

    /// <summary>
    /// The number sequence pay runs are numbered from.
    /// </summary>
    public const string PayRunNumberSeries = "Payroll:PayRuns";

    /// <summary>
    /// The number sequence pay statements are numbered from when they become final.
    /// </summary>
    public const string PayStatementNumberSeries = "Payroll:PayStatements";

    /// <summary>
    /// The notification type of a pay statement that is ready.
    /// </summary>
    public const string PayStatementReadyNotificationType = "PayStatementReady";

    /// <summary>
    /// The notification type that asks an employee to review their coming pay.
    /// </summary>
    public const string PayReviewRequestedNotificationType = "PayReviewRequested";

    /// <summary>
    /// The notification type that tells the preparer of a run an employee asked about their pay.
    /// </summary>
    public const string PayReviewQuestionedNotificationType = "PayReviewQuestioned";

    /// <summary>
    /// The notification type of a change to a person's tax elections or to where their pay goes: a security notice.
    /// </summary>
    public const string PayDetailsChangedNotificationType = "PayDetailsChanged";

    /// <summary>
    /// The notification type of a deposit of pay the bank returned.
    /// </summary>
    public const string DepositReturnedNotificationType = "DepositReturned";

    /// <summary>
    /// The notification type of a staff member's bank account added or changed: a security notice.
    /// </summary>
    public const string BankAccountChangedNotificationType = "BankAccountChanged";

    /// <summary>
    /// The editor group of a pay profile that a person edits in My Pay: their tax elections and net pay split only.
    /// </summary>
    public const string MyPayEditorGroup = "MyPay";

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
        /// Direct Deposit: net pay deposited to people's bank accounts through Disbursements.
        /// </summary>
        public const string DirectDeposit = "CrestApps.OrchardCore.Payroll.DirectDeposit";

        /// <summary>
        /// Paychecks: net pay paid by printed checks through Checks.
        /// </summary>
        public const string Paychecks = "CrestApps.OrchardCore.Payroll.Checks";

        /// <summary>
        /// My Pay: a person's own statements, tax elections and where their pay goes.
        /// </summary>
        public const string SelfService = "CrestApps.OrchardCore.Payroll.SelfService";

        /// <summary>
        /// Payroll AI Tools: tools an AI assistant uses to explain a person's own pay and what changed in a pay run.
        /// Off unless a site turns it on, because pay then goes to the site's AI provider.
        /// </summary>
        public const string AI = "CrestApps.OrchardCore.Payroll.AI";

        /// <summary>
        /// United States Payroll Filings: Forms 941, 940, 945, W-2 and W-3, 1099-NEC and 1096, the EFW2 file and year
        /// end, over Taxation's United States Income Tax.
        /// </summary>
        public const string UnitedStates = "CrestApps.OrchardCore.Payroll.UnitedStates";

        /// <summary>
        /// Canada Payroll Filings: source deductions remittances to the CRA and Revenu Québec, T4 and RL-1 slips with
        /// their summaries, and the T4 XML file, over Taxation's Canada Income Tax.
        /// </summary>
        public const string Canada = "CrestApps.OrchardCore.Payroll.Canada";

        /// <summary>
        /// Garnishments: orders withheld from pay within the law's limits and sent to their payees.
        /// </summary>
        public const string Garnishments = "CrestApps.OrchardCore.Payroll.Garnishments";
    }
}
