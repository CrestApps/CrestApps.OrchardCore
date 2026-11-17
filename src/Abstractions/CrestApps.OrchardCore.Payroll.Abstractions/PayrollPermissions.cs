using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// The permissions of Payroll. Every one but the two "own" ones and seeing the pay of managed staff also exists per
/// employer (<see cref="PayrollPermissionTemplates"/>), so a payroll clerk of one employer sees nothing of another.
/// </summary>
public static class PayrollPermissions
{
    /// <summary>
    /// Manage pay schedules and pay codes.
    /// </summary>
    public static readonly Permission ManagePaySetup = new("ManagePaySetup", LocalizationSource.Create("Manage pay setup", typeof(PayrollPermissions)));

    /// <summary>
    /// Manage employers, their registrations, ledger codes, tax setup and Payroll's settings.
    /// </summary>
    public static readonly Permission ManagePayrollSettings = new("ManagePayrollSettings", LocalizationSource.Create("Manage payroll settings and employers", typeof(PayrollPermissions)), isSecurityCritical: true);

    /// <summary>
    /// Change pay profiles, pay items and elections.
    /// </summary>
    public static readonly Permission ManagePayProfiles = new("ManagePayProfiles", LocalizationSource.Create("Manage pay profiles", typeof(PayrollPermissions)));

    /// <summary>
    /// See pay profiles, pay items and elections.
    /// </summary>
    public static readonly Permission ViewPayProfiles = new("ViewPayProfiles", LocalizationSource.Create("View pay profiles", typeof(PayrollPermissions)), [ManagePayProfiles]);

    /// <summary>
    /// See a person's full tax id. Every view is recorded.
    /// </summary>
    public static readonly Permission ViewTaxIds = new("ViewTaxIds", LocalizationSource.Create("View full tax ids", typeof(PayrollPermissions)), isSecurityCritical: true);

    /// <summary>
    /// Manage garnishment orders.
    /// </summary>
    public static readonly Permission ManageGarnishments = new("ManageGarnishments", LocalizationSource.Create("Manage garnishments", typeof(PayrollPermissions)));

    /// <summary>
    /// Open, enter, import and calculate pay runs.
    /// </summary>
    public static readonly Permission PreparePayRuns = new("PreparePayRuns", LocalizationSource.Create("Prepare pay runs", typeof(PayrollPermissions)));

    /// <summary>
    /// Decide a pay run's approval.
    /// </summary>
    public static readonly Permission ApprovePayRuns = new("ApprovePayRuns", LocalizationSource.Create("Approve pay runs", typeof(PayrollPermissions)));

    /// <summary>
    /// Finalize and pay pay runs: make bank files and print checks.
    /// </summary>
    public static readonly Permission ProcessPayRuns = new("ProcessPayRuns", LocalizationSource.Create("Process pay runs", typeof(PayrollPermissions)), isSecurityCritical: true);

    /// <summary>
    /// Void and reissue pay statements.
    /// </summary>
    public static readonly Permission VoidPayStatements = new("VoidPayStatements", LocalizationSource.Create("Void pay statements", typeof(PayrollPermissions)));

    /// <summary>
    /// See pay runs and every statement in them.
    /// </summary>
    public static readonly Permission ViewPayRuns = new("ViewPayRuns", LocalizationSource.Create("View pay runs", typeof(PayrollPermissions)), [PreparePayRuns, ApprovePayRuns, ProcessPayRuns, VoidPayStatements]);

    /// <summary>
    /// Read payroll data in data sets and pipelines, and export it, such as to an outside payroll system.
    /// </summary>
    public static readonly Permission ExportPayrollData = new("ExportPayrollData", LocalizationSource.Create("Export payroll data", typeof(PayrollPermissions)), isSecurityCritical: true);

    /// <summary>
    /// See payroll reports and the data sets behind them.
    /// </summary>
    public static readonly Permission ViewPayrollReports = new("ViewPayrollReports", LocalizationSource.Create("View payroll reports", typeof(PayrollPermissions)), [ExportPayrollData]);

    /// <summary>
    /// Manage tax liabilities, deposits, filings and year-end.
    /// </summary>
    public static readonly Permission ManagePayrollFilings = new("ManagePayrollFilings", LocalizationSource.Create("Manage payroll filings", typeof(PayrollPermissions)));

    /// <summary>
    /// A manager sees the pay of the staff they manage. Off for everyone by default.
    /// </summary>
    public static readonly Permission ViewPayOfManagedStaff = new("ViewPayOfManagedStaff", LocalizationSource.Create("View the pay of managed staff", typeof(PayrollPermissions)));

    /// <summary>
    /// See one's own pay statements and tax forms.
    /// </summary>
    public static readonly Permission ViewOwnPay = new("ViewOwnPay", LocalizationSource.Create("View own pay", typeof(PayrollPermissions)));

    /// <summary>
    /// Change one's own tax elections and bank accounts.
    /// </summary>
    public static readonly Permission ManageOwnPayDetails = new("ManageOwnPayDetails", LocalizationSource.Create("Manage own pay details", typeof(PayrollPermissions)));

    /// <summary>
    /// Gets the permissions that also exist per employer.
    /// </summary>
    public static IReadOnlyList<Permission> PerEmployer { get; } =
    [
        ManagePayrollSettings,
        ViewPayProfiles,
        ManagePayProfiles,
        ViewTaxIds,
        ManageGarnishments,
        PreparePayRuns,
        ApprovePayRuns,
        ProcessPayRuns,
        VoidPayStatements,
        ViewPayRuns,
        ViewPayrollReports,
        ExportPayrollData,
        ManagePayrollFilings,
    ];

    /// <summary>
    /// Gets every permission of the Payroll feature, without Pay Setup's.
    /// </summary>
    public static IReadOnlyList<Permission> Payroll { get; } =
    [
        .. PerEmployer,
        ViewPayOfManagedStaff,
        ViewOwnPay,
        ManageOwnPayDetails,
    ];
}
