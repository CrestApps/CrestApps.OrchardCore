using System.ComponentModel;
using System.Text.Json.Serialization;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a short-lived lock that reserves an activity for an agent before assignment is finalized.
/// </summary>
public sealed class ActivityReservation : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the identifier of the CRM activity that is reserved.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the queue the reservation originated from.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the queue item that is reserved.
    /// </summary>
    public string QueueItemId { get; set; }

    /// <summary>
    /// Gets or sets the dialer profile that dials the reserved item, when it is outbound dialer inventory.
    /// Copied from the queue item so the pacer and preview-accept path can apply the profile's settings without
    /// the profile owning a campaign.
    /// </summary>
    public string DialerProfileId { get; set; }

    /// <summary>
    /// Gets or sets the agent the activity is reserved for.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle status of the reservation.
    /// </summary>
    [JsonInclude]
    public ReservationStatus Status { get; private set; }

    /// <summary>
    /// Moves the ActivityReservation to the specified reservation status.
    /// </summary>
    /// <param name="status">The status to move to.</param>
    /// <exception cref="InvalidStateTransitionException">The ActivityReservation cannot reach the status from the one it is in.</exception>
    public void TransitionTo(ReservationStatus status)
    {
        if (!ReservationLifecycle.CanTransition(Status, status))
        {
            throw new InvalidStateTransitionException(nameof(ActivityReservation), Status, status);
        }

        Status = status;
    }

    /// <summary>
    /// Determines whether the ActivityReservation can move to the specified status.
    /// </summary>
    /// <param name="status">The status to test.</param>
    /// <returns><see langword="true"/> when the transition is admitted; otherwise <see langword="false"/>.</returns>
    public bool CanTransitionTo(ReservationStatus status)
        => ReservationLifecycle.CanTransition(Status, status);

    /// <summary>
    /// Gets a value indicating whether the reservation has resolved and no longer holds its lock.
    /// </summary>
    [JsonIgnore]
    public bool IsResolved => ReservationLifecycle.IsResolved(Status);

    /// <summary>
    /// Restores a status that was decided elsewhere, without consulting the lifecycle.
    /// </summary>
    /// <param name="status">The status to restore.</param>
    /// <returns>The same ActivityReservation, so it can be used at the end of an object initializer.</returns>
    /// <remarks>
    /// This bypasses every transition rule and exists only so a test can arrange a state directly. Production code
    /// must never call it: <c>AggregateLifecycleArchitectureTests</c> fails the build if any file under <c>src/</c>
    /// does, so the bypass cannot quietly become a shortcut.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ActivityReservation RestorePersistedStatus(ReservationStatus status)
    {
        Status = status;

        return this;
    }

    /// <summary>
    /// Gets or sets the UTC time the reservation was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the reservation expires when not accepted.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>
    /// Gets the number of times the agent has been granted more time on this offer.
    /// </summary>
    [JsonInclude]
    public int ExtensionCount { get; private set; }

    /// <summary>
    /// Grants the agent more time on this offer.
    /// </summary>
    /// <remarks>
    /// Refused once the offer is resolved -- an accepted, rejected or expired offer is no longer the agent's to
    /// hold -- and once the cap is reached, so more time cannot become indefinite. The extension is measured
    /// from whichever is later, the current expiry or now, so extending an offer that has just lapsed still
    /// grants the full period rather than a fraction of it.
    /// </remarks>
    /// <param name="extension">How much more time to grant.</param>
    /// <param name="maximumExtensions">The number of extensions this offer is allowed.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns><see langword="true"/> when the offer was extended; otherwise <see langword="false"/>.</returns>
    public bool Extend(TimeSpan extension, int maximumExtensions, DateTime nowUtc)
    {
        if (extension <= TimeSpan.Zero || IsResolved || ExtensionCount >= maximumExtensions)
        {
            return false;
        }

        var from = ExpiresUtc > nowUtc ? ExpiresUtc : nowUtc;

        ExpiresUtc = from.Add(extension);
        ExtensionCount++;

        return true;
    }

    /// <summary>
    /// Gets or sets the UTC time the reservation was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
