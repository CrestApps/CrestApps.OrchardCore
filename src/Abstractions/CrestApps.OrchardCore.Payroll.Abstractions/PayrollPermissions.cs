using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// The permissions of Payroll.
/// </summary>
public static class PayrollPermissions
{
    /// <summary>
    /// Pay schedules and pay codes.
    /// </summary>
    public static readonly Permission ManagePaySetup = new("ManagePaySetup", LocalizationSource.Create("Manage pay schedules and pay codes", typeof(PayrollPermissions)));

    /// <summary>
    /// Read the payroll data sets and export pay inputs, such as through a data pipeline.
    /// </summary>
    public static readonly Permission ExportPayrollData = new("ExportPayrollData", LocalizationSource.Create("Export payroll data", typeof(PayrollPermissions)), isSecurityCritical: true);
}
