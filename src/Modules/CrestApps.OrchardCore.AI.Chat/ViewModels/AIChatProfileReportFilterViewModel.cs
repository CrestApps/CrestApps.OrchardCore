using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// The AI profile filter of the AI chat reports.
/// </summary>
public class AIChatProfileReportFilterViewModel
{
    /// <summary>
    /// Gets or sets the chosen AI profile identifier.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets whether the report shows nothing until a profile is chosen.
    /// </summary>
    [BindNever]
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the chat AI profiles to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Profiles { get; set; } = [];
}
