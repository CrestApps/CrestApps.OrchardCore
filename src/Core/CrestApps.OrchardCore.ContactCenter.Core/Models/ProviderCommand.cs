using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a durable, idempotent record of a single provider command. The command intent is persisted
/// before the provider is contacted so a lost response, restart, or retry can be reconciled instead of
/// blindly re-issued, protecting the customer from a duplicate action.
/// </summary>
public sealed class ProviderCommand : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the stable idempotency key that uniquely identifies this command. Exactly one command
    /// exists per key within a tenant.
    /// </summary>
    public string CommandId { get; set; }

    /// <summary>
    /// Gets or sets the canonical provider technical name the command targets.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the kind of provider operation the command represents.
    /// </summary>
    public ProviderCommandType CommandType { get; set; }

    /// <summary>
    /// Gets or sets the current lifecycle status of the command.
    /// </summary>
    public ProviderCommandStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the monotonically increasing fence token. It increases on every claim so a stale worker
    /// or delayed provider response carrying an older fence can be rejected safely.
    /// </summary>
    public long FenceToken { get; set; }

    /// <summary>
    /// Gets or sets the opaque token identifying the worker that currently owns the command lease.
    /// </summary>
    public string OwnerToken { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the current claim lease expires. After this time another worker may reclaim
    /// the command.
    /// </summary>
    public DateTime LeaseExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the CRM activity identifier this command relates to, when applicable.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the interaction identifier this command relates to, when applicable.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the reservation identifier this command was issued under, when applicable. It is retained
    /// so a definitive failure or reconciliation can compensate the exact reservation rather than guessing.
    /// </summary>
    public string ReservationId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the reservation associated with this command should be removed
    /// from the queue when the command reaches a failure outcome. Defaults to <see langword="true"/> for
    /// backward-compatible Dial commands. Set to <see langword="false"/> for commands such as Answer where the
    /// reservation must remain in the queue so another agent can attempt the call.
    /// </summary>
    public bool RemoveReservationFromQueueOnFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets the dialer profile whose current compliance policy must be revalidated before recovering a
    /// pending outbound command.
    /// </summary>
    public string DialerProfileId { get; set; }

    /// <summary>
    /// Gets or sets the provider-assigned reference (for example a provider call identifier) captured once
    /// the outcome is confirmed.
    /// </summary>
    public string ProviderReference { get; set; }

    /// <summary>
    /// Gets or sets the serialized request payload retained so the command can be reconciled or replayed.
    /// </summary>
    public string RequestPayload { get; set; }

    /// <summary>
    /// Gets or sets the number of dispatch attempts made so far.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the number of reconciliation attempts made so far.
    /// </summary>
    public int ReconcileCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the next dispatch or reconciliation attempt is due.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets the reason or error captured from the most recent transition.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the command was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the command was dispatched to the provider, when applicable.
    /// </summary>
    public DateTime? SentUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the command reached a terminal state, when applicable.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the command was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets a value indicating whether the command is in a terminal state and can no longer transition.
    /// </summary>
    public bool IsTerminal => Status is ProviderCommandStatus.Confirmed
        or ProviderCommandStatus.Compensated
        or ProviderCommandStatus.Failed;
}
