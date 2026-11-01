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

    /// <summary>
    /// Post, assign and cancel the open shifts of the teams the user manages, and decide on claims.
    /// </summary>
    public static readonly Permission ManageOpenShifts = new("ManageWorkforceOpenShifts", LocalizationSource.Create("Manage open shifts", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Approve or decline the shift trades of the teams the user manages, and offer or accept trades on a worker's
    /// behalf.
    /// </summary>
    public static readonly Permission ApproveShiftTrades = new("ApproveWorkforceShiftTrades", LocalizationSource.Create("Approve shift trades", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Add, change and remove time-off types, policies, allotments and blackout dates.
    /// </summary>
    public static readonly Permission ManageTimeOffPolicies = new("ManageTimeOffPolicies", LocalizationSource.Create("Manage time-off policies", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Approve, decline and cancel the time off of the teams the user manages, and request time off for their workers.
    /// </summary>
    public static readonly Permission ApproveTimeOff = new("ApproveTimeOff", LocalizationSource.Create("Approve time off", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Change the time-off balances of the workers of the teams the user manages.
    /// </summary>
    public static readonly Permission AdjustTimeOffBalances = new("AdjustTimeOffBalances", LocalizationSource.Create("Adjust time-off balances", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Add, change and remove labor rule sets, and see the overrides of their rules.
    /// </summary>
    public static readonly Permission ManageLaborRules = new("ManageLaborRules", LocalizationSource.Create("Manage labor rules", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Go ahead, with a reason, with changes that break a labor rule that needs an override.
    /// </summary>
    public static readonly Permission OverrideLaborRules = new("OverrideLaborRules", LocalizationSource.Create("Override labor rules", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Approve or waive the predictability pay of the workers of the teams the user manages.
    /// </summary>
    public static readonly Permission ApprovePredictabilityPay = new("ApprovePredictabilityPay", LocalizationSource.Create("Approve predictability pay", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Use the My Work portal: one's own schedule, time off, shifts to claim and trade, and notifications.
    /// </summary>
    public static readonly Permission UseWorkforcePortal = new("UseWorkforcePortal", LocalizationSource.Create("Use the My Work portal", typeof(WorkforcePermissions)));

    /// <summary>
    /// Request one's own time off.
    /// </summary>
    public static readonly Permission RequestTimeOff = new("RequestTimeOff", LocalizationSource.Create("Request time off", typeof(WorkforcePermissions)));

    /// <summary>
    /// Manage time clock policies, pay periods, pay codes and kiosks.
    /// </summary>
    public static readonly Permission ManageTimeClockPolicies = new("ManageTimeClockPolicies", LocalizationSource.Create("Manage time clock policies", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Approve, reject, reopen and lock the timesheets of the workers of the teams the user manages, and decide their
    /// attendance exceptions and punch corrections.
    /// </summary>
    public static readonly Permission ApproveTimesheets = new("ApproveTimesheets", LocalizationSource.Create("Approve timesheets", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Add, change and remove the punches of the workers of the teams the user manages.
    /// </summary>
    public static readonly Permission EditPunches = new("EditPunches", LocalizationSource.Create("Edit punches", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Export locked timesheets for payroll.
    /// </summary>
    public static readonly Permission ExportPayroll = new("ExportPayroll", LocalizationSource.Create("Export payroll", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// See and change workers' pay rates.
    /// </summary>
    public static readonly Permission ViewPayRates = new("ViewPayRates", LocalizationSource.Create("View pay rates", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Manage planning groups, forecast events and forecasts, and make a forecast the team's coverage.
    /// </summary>
    public static readonly Permission ManageForecasts = new("ManageWorkforceForecasts", LocalizationSource.Create("Manage forecasts", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Manage adherence rule sets and exceptions.
    /// </summary>
    public static readonly Permission ManageAdherence = new("ManageWorkforceAdherence", LocalizationSource.Create("Manage adherence rules and exceptions", typeof(WorkforcePermissions)), [ManageAllWorkforce]);

    /// <summary>
    /// Watch the adherence of the agents in the teams one manages.
    /// </summary>
    public static readonly Permission ViewAdherence = new("ViewWorkforceAdherence", LocalizationSource.Create("View adherence", typeof(WorkforcePermissions)), [ManageAdherence]);

    /// <summary>
    /// Clock in and out, and see one's own timesheet, in the My Work portal.
    /// </summary>
    public static readonly Permission PunchTime = new("PunchTime", LocalizationSource.Create("Clock in and out", typeof(WorkforcePermissions)));
}
