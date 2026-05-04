using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Models;

/// <summary>
/// UI and admin-menu settings stored on an <see cref="AIProfile"/> of type <see cref="AIProfileType.Chat"/>.
/// </summary>
public sealed class AIChatProfileSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the profile is visible on the admin menu. This is only applicable to profiles with Chat type.
    /// </summary>
    public bool IsOnAdminMenu { get; set; }
}
