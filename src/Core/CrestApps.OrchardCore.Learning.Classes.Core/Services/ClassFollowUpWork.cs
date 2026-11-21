using CrestApps.Core.Models;
using CrestApps.OrchardCore.WorkQueues;

namespace CrestApps.OrchardCore.Learning.Classes.Core.Services;

/// <summary>
/// Work on a class registration that did not finish when its appointment changed, such as attendance the progress
/// service refused because the enrollment was busy, or a shift plan that was being changed. It is retried from a
/// leased queue with a back-off, and each attempt acts on the registration as it is then.
/// </summary>
public sealed class ClassFollowUpWork : CatalogItem, ILeasedWorkItem
{
    /// <summary>
    /// The collection of the queue.
    /// </summary>
    public const string CollectionName = "LearningClassFollowUps";

    /// <summary>
    /// Gets or sets the registration, a Scheduling appointment.
    /// </summary>
    public string AppointmentId { get; set; }

    /// <summary>
    /// Gets or sets what to do, one of <see cref="ClassFollowUpKinds"/>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the user id of the person whose change it follows.
    /// </summary>
    public string Actor { get; set; }

    /// <inheritdoc />
    public LeasedWorkItemStatus Status { get; set; }

    /// <inheritdoc />
    public string OwnerToken { get; set; }

    /// <inheritdoc />
    public long FenceToken { get; set; }

    /// <inheritdoc />
    public int AttemptCount { get; set; }

    /// <inheritdoc />
    public DateTime NextAttemptUtc { get; set; }

    /// <inheritdoc />
    public string LastError { get; set; }
}

/// <summary>
/// What a <see cref="ClassFollowUpWork"/> does.
/// </summary>
public static class ClassFollowUpKinds
{
    /// <summary>
    /// Brings the class item of the enrollment in line with the registration's attendance.
    /// </summary>
    public const string Attendance = "Attendance";

    /// <summary>
    /// Puts the class meetings on the staff member's shift plan, or takes them off.
    /// </summary>
    public const string ShiftSegments = "ShiftSegments";
}
