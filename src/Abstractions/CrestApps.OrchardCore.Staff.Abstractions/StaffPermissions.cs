using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Staff;

/// <summary>
/// The Staff permissions.
/// </summary>
public static class StaffPermissions
{
    /// <summary>
    /// Add, change and remove every staff member.
    /// </summary>
    public static readonly Permission ManageStaff = new("ManageStaff", LocalizationSource.Create("Manage staff", typeof(StaffPermissions)));

    /// <summary>
    /// Change the staff members a person manages: their direct and indirect reports and the members of the teams they manage.
    /// </summary>
    public static readonly Permission ManageOwnStaff = new("ManageOwnStaff", LocalizationSource.Create("Manage the staff you manage", typeof(StaffPermissions)), [ManageStaff]);

    /// <summary>
    /// See the staff list, the directory and the organization chart.
    /// </summary>
    public static readonly Permission ViewStaff = new("ViewStaff", LocalizationSource.Create("View staff", typeof(StaffPermissions)), [ManageStaff, ManageOwnStaff]);

    /// <summary>
    /// See one's own staff page: one's record, reporting line, teams and the links other modules add.
    /// </summary>
    public static readonly Permission ViewOwnStaffProfile = new("ViewOwnStaffProfile", LocalizationSource.Create("View one's own staff profile", typeof(StaffPermissions)), [ManageStaff]);

    /// <summary>
    /// See and change sensitive staff data, such as the date of birth.
    /// </summary>
    public static readonly Permission ViewSensitiveStaffData = new("ViewSensitiveStaffData", LocalizationSource.Create("View sensitive staff data", typeof(StaffPermissions)));

    /// <summary>
    /// See and change staff pay and cost rates.
    /// </summary>
    public static readonly Permission ManageStaffPay = new("ManageStaffPay", LocalizationSource.Create("Manage staff pay rates", typeof(StaffPermissions)));
}
