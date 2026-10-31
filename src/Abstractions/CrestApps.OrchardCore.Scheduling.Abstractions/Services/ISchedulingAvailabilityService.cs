using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.Services;

/// <summary>
/// Works out the start times an appointment type can be booked at: the resources' working hours (or shifts), less their
/// busy calendar time with the type's buffers, within the type's notice and horizon and its daily limit.
/// </summary>
public interface ISchedulingAvailabilityService
{
    /// <summary>
    /// Finds the start times in a range.
    /// </summary>
    /// <param name="appointmentType">The appointment type.</param>
    /// <param name="fromUtc">The start of the range.</param>
    /// <param name="toUtc">The end of the range.</param>
    /// <param name="resourceId">A resource to limit the times to, or <see langword="null"/>.</param>
    /// <param name="maxResults">The most times to return.</param>
    /// <param name="excludeAppointmentId">An appointment whose own time doesn't count as busy, for a reschedule.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The times, ordered by start.</returns>
    Task<IReadOnlyList<AppointmentSlot>> FindSlotsAsync(AppointmentType appointmentType, DateTime fromUtc, DateTime toUtc, string resourceId = null, int maxResults = 200, string excludeAppointmentId = null, CancellationToken cancellationToken = default);
}
