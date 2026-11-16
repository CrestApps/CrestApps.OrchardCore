using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Tickets.Agreements;

/// <summary>
/// The ticket terms of an agreement on its editor.
/// </summary>
public class TicketAgreementTermsViewModel
{
    /// <summary>
    /// Gets or sets whether the agreement covers tickets.
    /// </summary>
    public bool CoversTickets { get; set; }

    /// <summary>
    /// Gets or sets the desks it covers; none covers every desk.
    /// </summary>
    public List<string> DeskIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the ticket types it covers; none covers every type.
    /// </summary>
    public List<string> TicketTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the policy its tickets run on.
    /// </summary>
    public string ServiceLevelPolicyId { get; set; }

    [BindNever]
    public List<SelectListItem> Desks { get; set; } = [];

    [BindNever]
    public List<SelectListItem> Types { get; set; } = [];

    [BindNever]
    public List<SelectListItem> Policies { get; set; } = [];
}
