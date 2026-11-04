using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.WorkQueues;
using YesSql;

namespace CrestApps.OrchardCore.Webhooks.Core.Models;

/// <summary>
/// Represents an authenticated provider webhook delivery retained until processing succeeds or is dead-lettered.
/// </summary>
/// <remarks>
/// YesSql stores a document's type by name and finds a document by that name. The record moved here from Contact
/// Center, so it keeps the name it was first stored under: every delivery already in a tenant's database stays
/// readable, and nothing has to rewrite the stored rows.
/// <para>
/// It is an item of the shared leased work queue. Its stored <see cref="Status"/> keeps its own enum, whose values match
/// <see cref="LeasedWorkItemStatus"/>, so existing deliveries read unchanged.
/// </para>
/// </remarks>
[SimplifiedTypeName(StoredTypeName)]
public sealed class ProviderWebhookInboxMessage : CatalogItem, IModifiedUtcAwareModel, ILeasedWorkItem
{
    /// <summary>
    /// The type name inbox documents are stored under. It never changes.
    /// </summary>
    public const string StoredTypeName = "CrestApps.OrchardCore.ContactCenter.Core.Models.ProviderWebhookInboxMessage, CrestApps.OrchardCore.ContactCenter.Core";

    /// <summary>
    /// Gets or sets the canonical provider technical name.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider-scoped idempotency key.
    /// </summary>
    public string DeliveryId { get; set; }

    /// <summary>
    /// Gets or sets the stable technical name of the payload handler.
    /// </summary>
    public string HandlerName { get; set; }

    /// <summary>
    /// Gets or sets the normalized serialized payload.
    /// </summary>
    public string Payload { get; set; }

    /// <summary>
    /// Gets or sets the durable processing status.
    /// </summary>
    public ProviderWebhookInboxStatus Status { get; set; }

    /// <inheritdoc/>
    LeasedWorkItemStatus ILeasedWorkItem.Status
    {
        get => (LeasedWorkItemStatus)Status;
        set => Status = (ProviderWebhookInboxStatus)value;
    }

    /// <summary>
    /// Gets or sets the opaque token identifying the worker that owns the current claim.
    /// </summary>
    public string OwnerToken { get; set; }

    /// <summary>
    /// Gets or sets the monotonically increasing fence token assigned to the current claim.
    /// </summary>
    public long FenceToken { get; set; }

    /// <summary>
    /// Gets or sets the number of failed processing attempts.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the next processing attempt is due.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets the exception type from the last failed processing attempt.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the message was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the message reached a terminal outcome: completed, becoming an idempotency
    /// tombstone, or dead-lettered after exhausting its attempts.
    /// </summary>
    public DateTime? ProcessedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the message was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
