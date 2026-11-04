using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.Services;

/// <summary>
/// Narrows the bookable slots of an appointment type after its resources' hours are worked out, such as to the
/// times other capacity allows. Each filter can drop slots or lower what remains of them, never add any; the
/// booking itself is checked against the same slots, so a time a filter drops can't be booked.
/// </summary>
public interface IAppointmentSlotFilter
{
    /// <summary>
    /// Filters the slots.
    /// </summary>
    /// <param name="context">The appointment type and the booking being moved, if any.</param>
    /// <param name="slots">The slots found so far, by start.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The slots that stay.</returns>
    Task<IReadOnlyList<AppointmentSlot>> FilterAsync(AppointmentSlotFilterContext context, IReadOnlyList<AppointmentSlot> slots, CancellationToken cancellationToken = default);
}

/// <summary>
/// What the slots of <see cref="IAppointmentSlotFilter"/> are for.
/// </summary>
public sealed class AppointmentSlotFilterContext
{
    /// <summary>
    /// Gets the appointment type.
    /// </summary>
    public AppointmentType AppointmentType { get; init; }

    /// <summary>
    /// Gets the booking being moved, whose own place doesn't count against it, or <see langword="null"/>.
    /// </summary>
    public string ExcludeAppointmentId { get; init; }
}
