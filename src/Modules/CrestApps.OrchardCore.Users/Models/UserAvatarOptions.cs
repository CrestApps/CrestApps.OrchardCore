using System.ComponentModel;

namespace CrestApps.OrchardCore.Users.Models;

/// <summary>
/// Represents the user avatar options.
/// </summary>
public class UserAvatarOptions
{
    /// <summary>
    /// Gets or sets the required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether use default style.
    /// </summary>
    [DefaultValue(true)]
    public bool UseDefaultStyle { get; set; } = true;
}
