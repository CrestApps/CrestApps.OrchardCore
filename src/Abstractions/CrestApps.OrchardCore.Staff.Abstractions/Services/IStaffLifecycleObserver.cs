using CrestApps.OrchardCore.Staff.Models;

namespace CrestApps.OrchardCore.Staff.Services;

/// <summary>
/// Hears about changes to staff members after they are saved, so other modules can react: give access when someone is
/// hired, clean up when someone leaves (sign an agent out, release a phone extension, unassign future work), or follow
/// a new manager or team. Register one with <c>AddStaffLifecycleObserver</c>.
/// </summary>
public interface IStaffLifecycleObserver
{
    /// <summary>
    /// Called after a staff member is created or changed.
    /// </summary>
    /// <param name="context">What changed.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ChangedAsync(StaffChangedContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after a staff member is deleted.
    /// </summary>
    /// <param name="staffMember">The deleted staff member.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task DeletedAsync(StaffMember staffMember, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// What changed on a staff member.
/// </summary>
public sealed class StaffChangedContext
{
    /// <summary>
    /// Gets or sets the staff member after the change.
    /// </summary>
    public StaffMember StaffMember { get; set; }

    /// <summary>
    /// Gets or sets whether the record was just created.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the status before the change.
    /// </summary>
    public StaffStatus? PreviousStatus { get; set; }

    /// <summary>
    /// Gets or sets the user before the change.
    /// </summary>
    public string PreviousUserId { get; set; }

    /// <summary>
    /// Gets or sets the manager before the change.
    /// </summary>
    public string PreviousManagerId { get; set; }

    /// <summary>
    /// Gets or sets whether the team memberships changed.
    /// </summary>
    public bool TeamsChanged { get; set; }

    /// <summary>
    /// Gets or sets whether the person left with this change: terminated now, or a termination date reached.
    /// </summary>
    public bool Terminated { get; set; }

    /// <summary>
    /// Gets or sets whether the person is working after the change and was not before (hired, rehired or back).
    /// </summary>
    public bool Activated { get; set; }

    /// <summary>
    /// Gets whether the manager changed.
    /// </summary>
    public bool ManagerChanged => !IsNew && !string.Equals(PreviousManagerId ?? string.Empty, StaffMember?.ManagerId ?? string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// Gets whether the linked user changed.
    /// </summary>
    public bool UserChanged => !string.Equals(PreviousUserId ?? string.Empty, StaffMember?.UserId ?? string.Empty, StringComparison.Ordinal);
}
