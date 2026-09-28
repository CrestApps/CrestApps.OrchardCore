namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Contains internal storage, schema, and projection constants for the Contact Center module set. These
/// values are implementation details of how Contact Center persists and projects its data and are deliberately
/// kept out of the public Abstractions package so that changing an internal projection or schema version does
/// not force a public-package version bump for downstream consumers that only depend on the webhook and
/// integration contracts.
/// </summary>
internal static class ContactCenterStorage
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
    /// The maximum stored length, in characters, of a canonicalized telephony provider name. The same canonical
    /// value is persisted across several Contact Center index tables, so every migration that stores it must pin
    /// this width to keep the column definitions consistent and avoid a value that fits in one table but truncates
    /// in another.
    /// </summary>
    public const int ProviderNameLength = 128;
}
