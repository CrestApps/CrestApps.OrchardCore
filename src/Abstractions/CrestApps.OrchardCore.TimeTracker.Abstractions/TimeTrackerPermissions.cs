using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TimeTracker;

/// <summary>
/// The permissions of the Time Tracker module.
/// </summary>
public static class TimeTrackerPermissions
{
    /// <summary>
    /// Change the stages and categories.
    /// </summary>
    public static readonly Permission ManageTimeTrackerSettings = new("ManageTimeTrackerSettings", LocalizationSource.Create("Manage time stages and categories", typeof(TimeTrackerPermissions)));

    /// <summary>
    /// Add, change and remove anyone's time.
    /// </summary>
    public static readonly Permission ManageTimeEntries = new("ManageTimeEntries", LocalizationSource.Create("Edit anyone's time", typeof(TimeTrackerPermissions)), [ManageTimeTrackerSettings]);

    /// <summary>
    /// See anyone's time and the time report.
    /// </summary>
    public static readonly Permission ViewTimeEntries = new("ViewTimeEntries", LocalizationSource.Create("View anyone's time and the time report", typeof(TimeTrackerPermissions)), [ManageTimeEntries]);

    /// <summary>
    /// Clock in and out, switch stage and see one's own time.
    /// </summary>
    public static readonly Permission TrackOwnTime = new("TrackOwnTime", LocalizationSource.Create("Track one's own time", typeof(TimeTrackerPermissions)), [ViewTimeEntries]);
}
