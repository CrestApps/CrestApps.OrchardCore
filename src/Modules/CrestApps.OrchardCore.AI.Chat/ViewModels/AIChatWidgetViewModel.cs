using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for AI chat widget.
/// </summary>
public class AIChatWidgetViewModel
{
    /// <summary>
    /// Gets or sets the profile id.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the total history.
    /// </summary>
    public int? TotalHistory { get; set; }

    /// <summary>
    /// Gets or sets the max history allowed.
    /// </summary>
    [BindNever]
    public int MaxHistoryAllowed { get; set; }

    /// <summary>
    /// Gets or sets the profiles.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; }
}
