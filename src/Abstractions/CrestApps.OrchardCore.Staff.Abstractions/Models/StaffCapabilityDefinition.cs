namespace CrestApps.OrchardCore.Staff.Models;

/// <summary>
/// A capability as an admin keeps it: one they added under Staff &gt; Capabilities, or their changes to one a module
/// seeds (its label, description, order or whether it is offered).
/// </summary>
public sealed class StaffCapabilityDefinition
{
    /// <summary>
    /// Gets or sets the capability's key, stored on staff members. It never changes once saved.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the label shown in the editor, filters and pickers.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets what the capability means, shown under its checkbox.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the order in the editor and lists; lower comes first.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the capability is offered in the editor, filters and pickers. Staff
    /// members who have it keep it while it is off.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// A capability as it applies on the site: a module's default with the admin's changes, or one the admin added.
/// </summary>
public sealed class StaffCapabilityInfo
{
    /// <summary>
    /// Gets or sets the capability's key, stored on staff members.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the label.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the order.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the capability is offered.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an enabled module seeds the capability because its code relies on it:
    /// it can be relabeled or turned off, but not deleted or renamed.
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an admin changed a built-in capability's defaults.
    /// </summary>
    public bool IsCustomized { get; set; }
}
