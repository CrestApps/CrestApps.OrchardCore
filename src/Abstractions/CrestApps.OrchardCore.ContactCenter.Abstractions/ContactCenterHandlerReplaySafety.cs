namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Declares how a Contact Center message handler behaves when the same message is delivered more than
/// once. Contact Center dispatch is at-least-once, so every handler must state a machine-readable replay
/// contract that registration validation can enforce and reviewers can audit.
/// </summary>
public enum ContactCenterHandlerReplaySafety
{
    /// <summary>
    /// No replay contract was declared. Registration validation rejects this value so a handler can never
    /// silently default to an unproven idempotency claim.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Replaying the handler converges to the same observable state. The handler performs only
    /// last-write-wins projections, broadcasts, or reads and holds no accumulating side effect, so a
    /// duplicate delivery cannot corrupt state or double an effect.
    /// </summary>
    NaturallyIdempotent,

    /// <summary>
    /// The handler dedupes on the durable event identifier before applying a non-idempotent effect (such as
    /// incrementing a counter or triggering a workflow), so a duplicate delivery is a no-op.
    /// </summary>
    DeduplicatedByEventId,

    /// <summary>
    /// Idempotency is enforced by a durable downstream store keyed on a stable identity (for example a
    /// provider inbox uniqueness constraint or an optimistic-concurrency compare-and-set), so a duplicate
    /// delivery is rejected or collapsed by that store rather than by the handler itself.
    /// </summary>
    GuardedByDurableStore,
}
