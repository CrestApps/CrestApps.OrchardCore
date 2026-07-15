using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to query call sessions.
/// </summary>
public sealed class CallSessionIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the interaction the call session belongs to.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the CRM activity the call belongs to.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the provider name that owns the call.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider call identifier.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the portable, non-null claim key that enforces one call session per canonical
    /// provider-call identity. It is <c>{ProviderName}|{ProviderCallId}</c> when the session has a
    /// provider call identifier; otherwise it falls back to the globally unique item identifier so
    /// sessions without a provider call cannot collide.
    /// </summary>
    public string ProviderCallClaimKey { get; set; }

    /// <summary>
    /// Gets or sets the normalized call state.
    /// </summary>
    public ContactCenterCallState State { get; set; }

    /// <summary>
    /// Gets or sets the agent connected to the call.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the queue the call was delivered from.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the call session was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the call session ended.
    /// </summary>
    public DateTime? EndedUtc { get; set; }
}
