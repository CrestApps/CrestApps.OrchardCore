using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for edit profile tools.
/// </summary>
public class EditProfileToolsViewModel
{
    /// <summary>
    /// Gets or sets the tools.
    /// </summary>
    public Dictionary<string, ToolEntry[]> Tools { get; set; }
}
