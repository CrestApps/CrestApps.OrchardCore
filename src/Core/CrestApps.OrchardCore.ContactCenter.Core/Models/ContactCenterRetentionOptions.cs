namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Configures Contact Center data-governance retention windows. Every window is expressed in days and a value
/// of zero disables purging for that entity so its data is kept indefinitely. The floor settings can only make
/// retention more conservative (keep data longer); they never purge earlier than the configured window.
/// </summary>
public sealed class ContactCenterRetentionOptions
{
    /// <summary>
    /// The number of records deleted per purge batch when none is configured.
    /// </summary>
    public const int DefaultPurgeBatchSize = 500;

    /// <summary>
    /// The number of purge batches one retention cycle may run when none is configured. Together with
    /// <see cref="DefaultPurgeBatchSize"/> it lets a single cycle drain five million expired records.
    /// </summary>
    public const int DefaultMaxPurgeBatchesPerCycle = 10_000;

    /// <summary>
    /// Gets or sets the number of records deleted per purge batch. Each batch is committed before the next is
    /// read so a large drain never accumulates one unbounded transaction. Zero or less uses
    /// <see cref="DefaultPurgeBatchSize"/>.
    /// </summary>
    public int PurgeBatchSize { get; set; } = DefaultPurgeBatchSize;

    /// <summary>
    /// Gets or sets the number of purge batches one retention cycle may run for each entity, so a large table
    /// cannot starve the entities that drain after it. It
    /// bounds how long a cycle can hold the tenant busy. When a cycle stops because this budget ran out it
    /// says so in its report rather than truncating silently. Zero or less uses
    /// <see cref="DefaultMaxPurgeBatchesPerCycle"/>.
    /// </summary>
    public int MaxPurgeBatchesPerCycle { get; set; } = DefaultMaxPurgeBatchesPerCycle;

    /// <summary>
    /// Gets or sets the number of days to retain durable interaction events before they are purged. A value of
    /// zero disables purging entirely so events are kept indefinitely.
    /// </summary>
    public int InteractionEventRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the number of days to retain completed interactions. Only interactions that have ended are
    /// eligible; a live interaction is never purged no matter how long it has been running.
    /// </summary>
    public int InteractionRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the number of days to retain ended call sessions, measured from the time the call ended.
    /// </summary>
    public int CallSessionRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the number of days to retain queue items that have left the queue, measured from the time
    /// they were dequeued rather than enqueued so a long wait does not shorten the window.
    /// </summary>
    public int QueueItemRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the number of days to retain reservations that have reached a terminal state, measured from
    /// the time they settled. Neither creation nor expiry can serve as the age: an accepted reservation lives
    /// for as long as the work does and keeps an expiry in the future.
    /// </summary>
    public int ActivityReservationRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the number of days to retain routing work state that has not been touched. There is no
    /// terminal status to key on, so this window is measured from the last mutation alone. That is safe because
    /// a purged work state is recreated and re-seeded from the CRM activity on next access.
    /// </summary>
    public int WorkStateRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the number of days to retain outbox messages that have been completed or dead-lettered.
    /// </summary>
    public int OutboxMessageRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the number of days to retain provider webhook inbox messages that have been completed or
    /// dead-lettered.
    /// </summary>
    public int WebhookInboxMessageRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the number of days to retain provider commands that have reached a terminal state,
    /// measured from completion because neither the retry time nor the lease time advances once a command has
    /// finished.
    /// </summary>
    public int ProviderCommandRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the number of days to retain agent sessions, measured from the last heartbeat regardless of
    /// whether the session still claims to be online, so sessions abandoned by a crashed node are collected.
    /// </summary>
    public int AgentSessionRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the number of days to retain callback requests that have reached a terminal status,
    /// measured from the last modification rather than the scheduled time, because a callback booked far ahead
    /// and then canceled keeps a scheduled time in the future.
    /// </summary>
    public int CallbackRequestRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the number of days to retain daily event metrics.
    /// </summary>
    public int EventMetricRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the number of days to retain secure capture sessions that have reached a terminal state
    /// (completed, cancelled, or expired), measured from the time they settled. A collecting capture is never
    /// purged; only settled captures, which hold no raw sensitive value, are eligible.
    /// </summary>
    public int SecureCaptureRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the number of days to retain event deduplication markers. A marker may only be purged once
    /// no redelivery of the same event can still arrive, so this window is raised to the outbox delivery
    /// envelope described by <see cref="ProcessedEventDeliveryEnvelopeDays"/> when that envelope is longer.
    /// Purging a marker early makes an already-processed event look new and lets its side effect run twice.
    /// </summary>
    public int ProcessedEventRetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the longest time, in days, a redelivery of the same event can still arrive. It is derived
    /// from the outbox and webhook retry envelopes (maximum attempts multiplied by the maximum backoff) and
    /// acts as a floor beneath <see cref="ProcessedEventRetentionDays"/> and
    /// <see cref="WebhookInboxMessageRetentionDays"/>, both of which suppress a redelivered event.
    /// </summary>
    public double ProcessedEventDeliveryEnvelopeDays { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of days the durable event log must remain rebuildable. Because purging
    /// the event log destroys the ability to replay projections for the purged period, retention never purges
    /// events younger than this horizon even when <see cref="InteractionEventRetentionDays"/> is shorter. This
    /// guarantees projections can be rebuilt for at least this window. Zero applies no replay-horizon floor.
    /// </summary>
    public int ProjectionReplayHorizonDays { get; set; }

    /// <summary>
    /// Gets or sets a legal-hold floor, in days, below which business records are never purged regardless of
    /// the configured retention window. Raise it to satisfy a legal hold or regulatory minimum-retention
    /// obligation. Zero applies no legal-hold floor. It applies to the records that carry customer interaction
    /// history, not to the infrastructure tables that only carry delivery bookkeeping.
    /// </summary>
    public int LegalHoldMinimumDays { get; set; }
}
