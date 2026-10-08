using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// One row per leg of a <see cref="CallSession"/>, so a call can be found from any of its legs.
/// </summary>
/// <remarks>
/// The session index knows a call only by the provider's id for its first leg. Whatever reports on a later leg, such
/// as the agent's soft phone reporting the quality of the leg it holds, knows only that leg's id.
/// </remarks>
public sealed class CallSessionLegIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the provider identifier of the leg.
    /// </summary>
    public string ProviderLegId { get; set; }

    /// <summary>
    /// Gets or sets the part the leg plays in the call.
    /// </summary>
    public CallPartyRole Role { get; set; }

    /// <summary>
    /// Gets or sets the agent on the leg, for an agent leg.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the interaction the call belongs to.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets when the leg started.
    /// </summary>
    public DateTime StartedUtc { get; set; }
}
