using System.ComponentModel;
using System.Text.Json.Serialization;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the routing-owned work state for a single CRM activity. Contact Center reservation, offer,
/// assignment and dialer transitions write this aggregate instead of the CRM activity, so a concurrent CRM
/// edit and a routing transition can never invalidate one another.
/// </summary>
public sealed class ContactCenterWorkState : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the identifier of the CRM activity this work state belongs to.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the routing-owned assignment status of the activity.
    /// </summary>
    [JsonInclude]
    public ActivityAssignmentStatus AssignmentStatus { get; private set; }

    /// <summary>
    /// Moves the ContactCenterWorkState to the specified assignment status.
    /// </summary>
    /// <param name="status">The status to move to.</param>
    /// <exception cref="InvalidStateTransitionException">The ContactCenterWorkState cannot reach the status from the one it is in.</exception>
    public void TransitionTo(ActivityAssignmentStatus status)
    {
        if (!WorkAssignmentLifecycle.CanTransition(AssignmentStatus, status))
        {
            throw new InvalidStateTransitionException(nameof(ContactCenterWorkState), AssignmentStatus, status);
        }

        AssignmentStatus = status;
    }

    /// <summary>
    /// Determines whether the ContactCenterWorkState can move to the specified status.
    /// </summary>
    /// <param name="status">The status to test.</param>
    /// <returns><see langword="true"/> when the transition is admitted; otherwise <see langword="false"/>.</returns>
    public bool CanTransitionTo(ActivityAssignmentStatus status)
        => WorkAssignmentLifecycle.CanTransition(AssignmentStatus, status);

    /// <summary>
    /// Adopts the routing status carried by the CRM activity this work state is seeded from.
    /// </summary>
    /// <param name="status">The assignment status the activity already carries.</param>
    /// <remarks>
    /// Seeding is not a transition. The work state is being created to mirror an activity that already holds a
    /// routing status decided before this record existed, so there is no previous status to move from and no
    /// edge to check. Treating it as a transition would refuse every activity that is already assigned.
    /// </remarks>
    public void AdoptActivityAssignmentStatus(ActivityAssignmentStatus status)
        => AssignmentStatus = status;

    /// <summary>
    /// Restores a status that was decided elsewhere, without consulting the lifecycle.
    /// </summary>
    /// <param name="status">The status to restore.</param>
    /// <returns>The same ContactCenterWorkState, so it can be used at the end of an object initializer.</returns>
    /// <remarks>
    /// This bypasses every transition rule and exists only so a test can arrange a state directly. Production code
    /// must never call it: <c>AggregateLifecycleArchitectureTests</c> fails the build if any file under <c>src/</c>
    /// does, so the bypass cannot quietly become a shortcut.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ContactCenterWorkState RestorePersistedAssignmentStatus(ActivityAssignmentStatus status)
    {
        AssignmentStatus = status;

        return this;
    }

    /// <summary>
    /// Gets or sets the identifier of the reservation currently holding the activity.
    /// </summary>
    public string ReservationId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier of the agent the activity is reserved for.
    /// </summary>
    public string ReservedById { get; set; }

    /// <summary>
    /// Gets or sets the user name of the agent the activity is reserved for.
    /// </summary>
    public string ReservedByUsername { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the activity was reserved.
    /// </summary>
    public DateTime? ReservedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the current reservation expires when it is not accepted.
    /// </summary>
    public DateTime? ReservationExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the user identifier of the agent the activity is assigned to.
    /// </summary>
    public string AssignedToId { get; set; }

    /// <summary>
    /// Gets or sets the user name of the agent the activity is assigned to.
    /// </summary>
    public string AssignedToUsername { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the activity was assigned.
    /// </summary>
    public DateTime? AssignedToUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of outbound attempts the dialer has made for the activity.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the work state was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the work state was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
