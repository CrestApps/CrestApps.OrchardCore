namespace CrestApps.OrchardCore.Staff.Models;

/// <summary>
/// The Staff site settings.
/// </summary>
public sealed class StaffSettings
{
    /// <summary>
    /// Gets or sets whether people pickers that ask for the Staff audience (activity assignees, lead owners, phone
    /// extensions, time entries and so on) offer only working staff. Off by default, so a site keeps offering every user
    /// until its staff are set up.
    /// </summary>
    public bool LimitPickersToStaff { get; set; }
}
