using OrchardCore.ContentManagement;
using OrchardCore.Media.Fields;

namespace CrestApps.OrchardCore.Users.Models;

/// <summary>
/// Represents the user avatar part.
/// </summary>
public sealed class UserAvatarPart : ContentPart
{
    /// <summary>
    /// Gets or sets the avatar.
    /// </summary>
    public MediaField Avatar { get; set; }
}
