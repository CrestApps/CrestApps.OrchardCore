using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The editor of a ticket type's Ticket part settings.
/// </summary>
public class TicketPartSettingsViewModel
{
    public string DefaultDeskId { get; set; }

    public string DefaultTeamId { get; set; }

    public string DefaultPriorityId { get; set; }

    public string ServiceLevelPolicyId { get; set; }

    public List<string> RequiredSkillIds { get; set; } = [];

    public bool CustomerSubmittable { get; set; }

    public string PortalDescription { get; set; }

    [BindNever]
    public List<SelectListItem> Desks { get; set; } = [];

    [BindNever]
    public List<SelectListItem> Priorities { get; set; } = [];

    [BindNever]
    public List<SelectListItem> Policies { get; set; } = [];

    /// <summary>
    /// Gets or sets the skills that may be required, or <see langword="null"/> when Skills is off.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Skills { get; set; }
}
