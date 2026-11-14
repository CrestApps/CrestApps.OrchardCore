using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// The settings of a ticket type: what a new ticket of the type starts with.
/// </summary>
public sealed class TicketPartSettings
{
    /// <summary>
    /// Gets or sets the desk a new ticket of the type goes to when none is given.
    /// </summary>
    public string DefaultDeskId { get; set; }

    /// <summary>
    /// Gets or sets the team a new ticket goes to, before the desk's.
    /// </summary>
    public string DefaultTeamId { get; set; }

    /// <summary>
    /// Gets or sets the priority a new ticket starts with.
    /// </summary>
    public string DefaultPriorityId { get; set; }

    /// <summary>
    /// Gets or sets the service-level policy of the type, before the desk's.
    /// </summary>
    public string ServiceLevelPolicyId { get; set; }

    /// <summary>
    /// Gets or sets the skills an assignee needs.
    /// </summary>
    public List<SkillRequirement> RequiredSkills { get; set; } = [];

    /// <summary>
    /// Gets or sets whether customers may submit the type from the portal.
    /// </summary>
    public bool CustomerSubmittable { get; set; }

    /// <summary>
    /// Gets or sets what the portal says about the type.
    /// </summary>
    public string PortalDescription { get; set; }
}
