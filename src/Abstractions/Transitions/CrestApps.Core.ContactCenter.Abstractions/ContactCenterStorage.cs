namespace CrestApps.Core.ContactCenter;

/// <summary>
/// Contains internal storage, schema, and projection constants for the Contact Center module set. These
/// values are implementation details of how Contact Center persists and projects its data and were kept out of the public surface
/// so that changing an internal projection or schema version did not force a version bump for consumers that
/// only depend on the webhook and integration contracts. They are public now because the indexes, migrations
/// and projections that need them are in other assemblies; the reason to treat them as implementation detail
/// stands, and the public-surface baseline is what records a change to one.
/// </summary>
public static class ContactCenterStorage
{
    /// <summary>
    /// The YesSql collection name used to store Contact Center documents in isolation from other modules.
    /// </summary>
    public const string CollectionName = "ContactCenter";

    /// <summary>
    /// The current schema version applied to newly published Contact Center domain events.
    /// </summary>
    public const int CurrentEventSchemaVersion = 1;

    /// <summary>
    /// The stable, versioned identifier of the daily event-count metrics projection. It namespaces the
    /// projection's deduplication markers and replay checkpoint, so its value must never change for a given
    /// projection logic version.
    /// </summary>
    public const string MetricsProjectionHandlerId = "ContactCenter/MetricsProjection/v1";

    /// <summary>
    /// The projection logic version of the daily event-count metrics projection. Bumping it forces a full
    /// replay because the stored checkpoint version no longer matches.
    /// </summary>
    public const int MetricsProjectionVersion = 1;

    /// <summary>
    /// The number of due provider webhook deliveries a store reads when the caller names no count of its own.
    /// </summary>
    /// <remarks>
    /// The dispatcher that drains the inbox asks for this many per pass, and it is the store that has to apply
    /// it when a caller passes nothing, so the number lives where both can see it rather than on the dispatcher
    /// the store would otherwise have to reach into.
    /// </remarks>
    public const int WebhookInboxBatchSize = 100;

    /// <summary>
    /// The number of settled provider webhook deliveries a store reads for one tombstone cleanup pass when the
    /// caller names no count of its own.
    /// </summary>
    public const int WebhookInboxTombstoneCleanupBatchSize = 100;

    /// <summary>
    /// The number of days a settled provider webhook delivery is kept as an idempotency tombstone.
    /// </summary>
    /// <remarks>
    /// A provider redelivery arriving within this window is recognised as a duplicate purely because the row is
    /// still there, so retention must never delete one sooner. The floor lives here because it is retention -
    /// which deletes the row - that has to honour it, not only the dispatcher that wrote it.
    /// </remarks>
    public const double WebhookInboxTombstoneRetentionDays = 7;

    /// <summary>
    /// The maximum stored length, in characters, of a canonicalized telephony provider name. The same canonical
    /// value is persisted across several Contact Center index tables, so every migration that stores it must pin
    /// this width to keep the column definitions consistent and avoid a value that fits in one table but truncates
    /// in another.
    /// </summary>
    public const int ProviderNameLength = 128;
}
