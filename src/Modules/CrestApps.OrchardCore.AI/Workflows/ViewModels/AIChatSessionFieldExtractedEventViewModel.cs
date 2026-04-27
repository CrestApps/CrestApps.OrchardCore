using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// Represents the view model for AI chat session field extracted event.
/// </summary>
public class AIChatSessionFieldExtractedEventViewModel
{
    /// <summary>
    /// Gets or sets the profile id.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the profiles.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; }
}
