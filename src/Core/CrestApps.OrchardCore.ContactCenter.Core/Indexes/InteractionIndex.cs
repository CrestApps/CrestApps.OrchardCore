using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to query interactions.
/// </summary>
public sealed class InteractionIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the channel the interaction is conducted on.
    /// </summary>
    public InteractionChannel Channel { get; set; }

    /// <summary>
    /// Gets or sets the direction of the interaction.
    /// </summary>
    public InteractionDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle status of the interaction.
    /// </summary>
    public InteractionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the CRM activity the interaction is linked to.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the provider name that produced the interaction.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider interaction or call identifier.
    /// </summary>
    public string ProviderInteractionId { get; set; }

    /// <summary>
    /// Gets or sets the provider call leg identifier.
    /// </summary>
    public string ProviderLegId { get; set; }

    /// <summary>
    /// Gets or sets the queue that handled the interaction, when applicable.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the agent connected to the interaction.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier of the interaction.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the interaction was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the interaction ended.
    /// </summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time after-call wrap-up started.
    /// </summary>
    public DateTime? WrapUpStartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time after-call wrap-up was completed.
    /// </summary>
    public DateTime? WrapUpCompletedUtc { get; set; }
}
