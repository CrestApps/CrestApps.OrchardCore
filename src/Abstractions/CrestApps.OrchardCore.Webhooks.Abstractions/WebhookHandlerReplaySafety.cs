namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Declares how a provider webhook inbox handler behaves when the same persisted payload is dispatched more than once.
/// Provider delivery is at-least-once, so every handler states a machine-readable replay contract that registration
/// validation enforces and reviewers can audit.
/// </summary>
public enum WebhookHandlerReplaySafety
{
    /// <summary>
    /// No replay contract was declared. Registration validation rejects this value so a handler can never silently
    /// default to an unproven idempotency claim.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Replaying the handler converges to the same observable state: it performs only last-write-wins projections,
    /// broadcasts or reads, so a duplicate delivery cannot corrupt state or double an effect.
    /// </summary>
    NaturallyIdempotent,

    /// <summary>
    /// The handler de-duplicates on the durable event identifier before applying a non-idempotent effect, so a
    /// duplicate delivery is a no-op.
    /// </summary>
    DeduplicatedByEventId,

    /// <summary>
    /// Idempotency is enforced by a durable downstream store keyed on a stable identity (a uniqueness constraint or an
    /// optimistic-concurrency compare-and-set), so a duplicate delivery is rejected or collapsed by that store.
    /// </summary>
    GuardedByDurableStore,
}
