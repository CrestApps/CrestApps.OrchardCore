using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to query agent profiles.
/// </summary>
public sealed class AgentProfileIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the unique name of the agent profile.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user the profile represents.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the current presence state of the agent.
    /// </summary>
    public AgentPresenceStatus PresenceStatus { get; set; }
}
