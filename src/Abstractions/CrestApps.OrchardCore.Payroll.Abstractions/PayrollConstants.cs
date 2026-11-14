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
    }
}
