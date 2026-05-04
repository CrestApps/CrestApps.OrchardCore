using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Roles.Core.Models;

/// <summary>
/// Represents the role picker part.
/// </summary>
public sealed class RolePickerPart : ContentPart
{
    /// <summary>
    /// Gets or sets the role names.
    /// </summary>
    public string[] RoleNames { get; set; }
}
