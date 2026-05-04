using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for test profile.
/// </summary>
public class TestProfileViewModel
{
    /// <summary>
    /// Gets or sets the profile id.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the profile.
    /// </summary>
    public AIProfile Profile { get; set; }
}
