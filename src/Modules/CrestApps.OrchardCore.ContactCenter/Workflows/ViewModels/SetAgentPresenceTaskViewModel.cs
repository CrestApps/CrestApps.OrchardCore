using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.ViewModels;

/// <summary>
/// Represents the edit view model for the <c>SetAgentPresenceTask</c> workflow activity.
/// </summary>
public class SetAgentPresenceTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression that resolves the agent's Orchard user identifier.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the presence status to apply.
    /// </summary>
    public AgentPresenceStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the optional Liquid expression that resolves the reason recorded with the change.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the selectable presence statuses.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Statuses { get; set; }
}
