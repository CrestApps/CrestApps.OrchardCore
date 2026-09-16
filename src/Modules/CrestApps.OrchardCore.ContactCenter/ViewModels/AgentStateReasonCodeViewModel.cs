using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the edit view model for an agent state reason code.
/// </summary>
public class AgentStateReasonCodeViewModel
{
    /// <summary>
    /// Gets or sets the reason code identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the unique reason code name.
    /// </summary>
    [Required]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the reason code description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the presence state the agent enters when selecting this reason code.
    /// </summary>
    public AgentPresenceStatus AppliesTo { get; set; } = AgentPresenceStatus.Break;

    /// <summary>
    /// Gets or sets the presence states a reason code may map to.
    /// </summary>
    public IList<SelectListItem> AppliesToOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the relative order the reason code is listed in.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the reason code is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
