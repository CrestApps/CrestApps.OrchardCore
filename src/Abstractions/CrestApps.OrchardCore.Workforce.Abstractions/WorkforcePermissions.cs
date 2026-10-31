using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// The Workforce permissions. Managers also see and change only the teams they manage, and those teams' sub-teams.
/// </summary>
public static class WorkforcePermissions
{
    /// <summary>
    /// Every team and every worker, regardless of who manages them.
    /// </summary>
    public static readonly Permission ManageAllWorkforce = new("ManageAllWorkforce", LocalizationSource.Create("Manage the whole workforce", typeof(WorkforcePermissions)));

    /// <summary>
    /// Add, change and remove teams.
    /// </summary>
    public static readonly Permission ManageTeams = new("ManageWorkforceTeams", LocalizationSource.Create("Manage workforce teams", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Add, change and remove the workers of the teams one manages.
    /// </summary>
    public static readonly Permission ManageWorkers = new("ManageWorkers", LocalizationSource.Create("Manage workers", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// See the workers of the teams one manages.
    /// </summary>
    public static readonly Permission ViewWorkers = new("ViewWorkers", LocalizationSource.Create("View workers", typeof(WorkforcePermissions)), [ManageWorkers]);

    /// <summary>
    /// See and change workers' sensitive details, such as their date of birth.
    /// </summary>
    public static readonly Permission ViewSensitiveWorkerData = new("ViewSensitiveWorkerData", LocalizationSource.Create("View sensitive worker details", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Add, change and remove activity codes.
    /// </summary>
    public static readonly Permission ManageActivityCodes = new("ManageActivityCodes", LocalizationSource.Create("Manage activity codes", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Add, change and remove shift templates.
    /// </summary>
    public static readonly Permission ManageShiftTemplates = new("ManageShiftTemplates", LocalizationSource.Create("Manage shift templates", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Publish the schedules of the teams the user manages.
    /// </summary>
    public static readonly Permission PublishSchedules = new("PublishWorkforceSchedules", LocalizationSource.Create("Publish team schedules", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Plan the schedules, rotations and coverage of the teams the user manages.
    /// </summary>
    public static readonly Permission ManageSchedules = new("ManageWorkforceSchedules", LocalizationSource.Create("Plan team schedules", typeof(WorkforcePermissions)), [PublishSchedules]);
}
