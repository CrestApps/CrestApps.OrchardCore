using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The AI assistant of a desk.
/// </summary>
public class TicketDeskAIViewModel
{
    /// <summary>
    /// Gets or sets the chat AI profile, or <see langword="null"/> for none.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets whether a triage sets the priority it suggests.
    /// </summary>
    public bool ApplyPriority { get; set; }

    /// <summary>
    /// Gets or sets the profiles to choose from.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Profiles { get; set; } = [];
}
