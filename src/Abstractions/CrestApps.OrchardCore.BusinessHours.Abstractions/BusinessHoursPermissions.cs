using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.BusinessHours;

/// <summary>
/// The permissions of the Business Hours module.
/// </summary>
public static class BusinessHoursPermissions
{
    /// <summary>
    /// Lets a user create, edit and delete business hours schedules.
    /// </summary>
    public static readonly Permission ManageBusinessHoursSchedules = new(
        "ManageBusinessHoursSchedules",
        LocalizationSource.Create("Manage business hours schedules", typeof(BusinessHoursPermissions)));
}
