using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The AI card of Settings &gt; Service Desk.
/// </summary>
public class TicketAISettingsViewModel
{
    /// <summary>
    /// Gets or sets the chat AI profile for suggested replies and triage, or <see langword="null"/> for none.
    /// </summary>
    public string ReplyProfileId { get; set; }

    /// <summary>
    /// Gets or sets the profiles to choose from.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Profiles { get; set; } = [];
}
