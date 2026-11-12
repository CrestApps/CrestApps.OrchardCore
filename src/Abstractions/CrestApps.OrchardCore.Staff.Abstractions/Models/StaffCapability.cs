using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Staff.Models;

/// <summary>
/// A kind of work a module lets staff members do, such as handling calls, field work or running a register.
/// Modules register their capabilities with <c>AddStaffCapability</c>; Staff never names them.
/// </summary>
public sealed class StaffCapability
{
    /// <summary>
    /// Gets or sets the capability name stored on staff members.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name shown in the editor and filters.
    /// </summary>
    public LocalizationSource DisplayName { get; set; }

    /// <summary>
    /// Gets or sets what the capability lets the person do.
    /// </summary>
    public LocalizationSource Description { get; set; }

    /// <summary>
    /// Gets or sets the order in the editor.
    /// </summary>
    public int Position { get; set; }
}

/// <summary>
/// The capabilities modules registered.
/// </summary>
public sealed class StaffOptions
{
    /// <summary>
    /// Gets the registered capabilities, by name.
    /// </summary>
    public Dictionary<string, StaffCapability> Capabilities { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets other modules' permissions that let a person manage every staff member, such as a module-wide manager
    /// permission. <see cref="StaffPermissions.ManageStaff"/> always does.
    /// </summary>
    public List<Permission> ManageEveryonePermissions { get; } = [];

    /// <summary>
    /// Gets other modules' permissions that let a person manage the staff they manage (their reports and the members of
    /// the teams they manage). <see cref="StaffPermissions.ManageOwnStaff"/> always does.
    /// </summary>
    public List<Permission> ManageOwnPermissions { get; } = [];
}
