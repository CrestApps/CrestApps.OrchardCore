namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Describes the intent required to register a new <see cref="ProviderCommand"/> before the provider is
/// contacted.
/// </summary>
public sealed class ProviderCommandRegistration
{
    /// <summary>
    /// Gets or sets the stable idempotency key that uniquely identifies the command.
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
    /// Gets or sets the CRM activity identifier the command relates to, when applicable.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the interaction identifier the command relates to, when applicable.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the reservation identifier the command is issued under, when applicable. It is retained
    /// so a definitive failure or reconciliation can compensate the exact reservation.
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
    /// Gets or sets the dialer profile whose current compliance policy must be revalidated before recovery
    /// dispatches a pending command.
    /// </summary>
    public string DialerProfileId { get; set; }

    /// <summary>
    /// Gets or sets the serialized request payload retained so the command can be reconciled or replayed.
    /// </summary>
    public string RequestPayload { get; set; }
}
