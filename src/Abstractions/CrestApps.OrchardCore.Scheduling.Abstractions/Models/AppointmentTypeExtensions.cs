namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// The rules an <see cref="AppointmentType"/> sets, worked out the same way everywhere they are applied or shown.
/// </summary>
public static class AppointmentTypeExtensions
{
    /// <summary>
    /// Gets whether a customer can still cancel an appointment themselves, given the type's cancel notice.
    /// </summary>
    /// <param name="appointmentType">The appointment type, or <see langword="null"/> when it no longer exists.</param>
    /// <param name="appointment">The appointment.</param>
    /// <param name="nowUtc">The current time, in UTC.</param>
    /// <returns><see langword="true"/> when the start is far enough away, or the type sets no notice.</returns>
    public static bool CustomerCanCancel(this AppointmentType appointmentType, Appointment appointment, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        return IsOutsideNotice(appointmentType?.CancelNoticeMinutes ?? 0, appointment.StartUtc, nowUtc);
    }

    /// <summary>
    /// Gets whether a customer can still move an appointment themselves, given the type's reschedule notice.
    /// </summary>
    /// <param name="appointmentType">The appointment type, or <see langword="null"/> when it no longer exists.</param>
    /// <param name="appointment">The appointment.</param>
    /// <param name="nowUtc">The current time, in UTC.</param>
    /// <returns><see langword="true"/> when the start is far enough away, or the type sets no notice.</returns>
    public static bool CustomerCanReschedule(this AppointmentType appointmentType, Appointment appointment, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        return IsOutsideNotice(appointmentType?.RescheduleNoticeMinutes ?? 0, appointment.StartUtc, nowUtc);
    }

    private static bool IsOutsideNotice(int noticeMinutes, DateTime startUtc, DateTime nowUtc)
    {
        return noticeMinutes <= 0 || startUtc - nowUtc >= TimeSpan.FromMinutes(noticeMinutes);
    }
}
