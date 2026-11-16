using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The AI assistant of a desk.
/// </summary>
public class TicketDeskAIViewModel
{
    /// <summary>
    /// The value of the choice that turns the assistant off for the desk.
    /// </summary>
    public const string Off = "off";

    /// <summary>
    /// Gets or sets the chat AI profile, <see langword="null"/> for the site's, or <see cref="Off"/>.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the name of the site's profile (Settings &gt; Service Desk), if any.
    /// </summary>
    [BindNever]
    public string SiteProfileName { get; set; }

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
