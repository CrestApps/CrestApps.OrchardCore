using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// Represents the view model for interaction document settings.
/// </summary>
public class InteractionDocumentSettingsViewModel
{
    /// <summary>
    /// Gets or sets the index profile name.
    /// </summary>
    public string IndexProfileName { get; set; }

    /// <summary>
    /// Gets or sets the index profiles.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> IndexProfiles { get; set; }
}
