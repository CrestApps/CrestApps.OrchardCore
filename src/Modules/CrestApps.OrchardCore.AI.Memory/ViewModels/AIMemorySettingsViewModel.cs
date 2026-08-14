using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Memory.ViewModels;

/// <summary>
/// Represents the view model for AI memory settings.
/// </summary>
public class AIMemorySettingsViewModel
{
    /// <summary>
    /// Gets or sets the index profile name.
    /// </summary>
    public string IndexProfileName { get; set; }

    /// <summary>
    /// Gets or sets the top n.
    /// </summary>
    public int TopN { get; set; } = 5;

    /// <summary>
    /// Gets or sets the index profiles.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> IndexProfiles { get; set; } = [];
}
