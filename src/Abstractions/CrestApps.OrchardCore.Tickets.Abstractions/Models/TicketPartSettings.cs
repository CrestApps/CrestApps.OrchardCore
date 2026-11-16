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

    /// <summary>
    /// Gets or sets the taxonomy the type's tickets take their category from; empty when the type has no categories.
    /// The taxonomy's terms may carry a <see cref="TicketCategoryPart"/> with the routing of their tickets.
    /// </summary>
    public string CategoryTaxonomyId { get; set; }

    /// <summary>
    /// Gets or sets whether a ticket of the type needs a category when it is opened from a form (the agents' page, the
    /// portal, the web form). Tickets opened through the API, workflows or e-mail may have none.
    /// </summary>
    public bool CategoryRequired { get; set; }

    /// <summary>
    /// Gets or sets whether only a category without sub-categories may be chosen (the most specific one).
    /// </summary>
    public bool CategoryLeavesOnly { get; set; }
}
