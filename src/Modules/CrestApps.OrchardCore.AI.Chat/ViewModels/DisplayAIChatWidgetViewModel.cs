using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for display AI chat widget.
/// </summary>
public class DisplayAIChatWidgetViewModel
{
    /// <summary>
    /// Gets or sets the sessions.
    /// </summary>
    [BindNever]
    public IEnumerable<AIChatSessionEntry> Sessions { get; set; }
}
