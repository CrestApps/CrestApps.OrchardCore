using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a canonical agent state reason code. Reason codes give agents auditable, admin-defined
/// reasons for entering a not-ready or break presence state, and they map each reason to the underlying
/// <see cref="AgentPresenceStatus"/> the agent should be placed in.
/// </summary>
public sealed class AgentStateReasonCode : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the unique reason code name shown to agents and supervisors.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the reason code description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the presence state an agent enters when they select this reason code.
    /// </summary>
    public AgentPresenceStatus AppliesTo { get; set; } = AgentPresenceStatus.Break;

    /// <summary>
    /// Gets or sets the relative order the reason code is listed in, lowest first.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the reason code can be selected by agents.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the reason code was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the reason code was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
