using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Scheduling;

/// <summary>
/// The scheduling permissions.
/// </summary>
public static class SchedulingPermissions
{
    /// <summary>
    /// Set up appointment types and resources.
    /// </summary>
    public static readonly Permission ManageScheduling = new("ManageScheduling", LocalizationSource.Create("Manage scheduling", typeof(SchedulingPermissions)));

    /// <summary>
    /// Book, change and cancel appointments for anyone.
    /// </summary>
    public static readonly Permission ManageAppointments = new("ManageAppointments", LocalizationSource.Create("Manage appointments", typeof(SchedulingPermissions)), [ManageScheduling]);

    /// <summary>
    /// See appointments.
    /// </summary>
    public static readonly Permission ViewAppointments = new("ViewAppointments", LocalizationSource.Create("View appointments", typeof(SchedulingPermissions)), [ManageAppointments]);
}
