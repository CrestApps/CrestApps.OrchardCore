namespace CrestApps.OrchardCore.Roles.Core.Models;

/// <summary>
/// Represents the role picker part settings.
/// </summary>
public sealed class RolePickerPartSettings
{
    /// <summary>
    /// Gets or sets the required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether allow select multiple.
    /// </summary>
    public bool AllowSelectMultiple { get; set; }

    /// <summary>
    /// Gets or sets the excluded roles.
    /// </summary>
    public string[] ExcludedRoles { get; set; }

    /// <summary>
    /// Gets or sets the hint.
    /// </summary>
    public string Hint { get; set; }
}
