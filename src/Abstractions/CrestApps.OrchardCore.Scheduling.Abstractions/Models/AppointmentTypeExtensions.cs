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

    /// <summary>
    /// Gets the groups of resources each appointment of the type needs. A type without <see cref="AppointmentType.ResourceRequirements"/>
    /// has one requirement made of its <see cref="AppointmentType.ResourceIds"/> and <see cref="AppointmentType.Assignment"/>.
    /// </summary>
    /// <param name="appointmentType">The appointment type.</param>
    /// <returns>The requirements, each with at least one resource.</returns>
    public static IReadOnlyList<AppointmentResourceRequirement> GetResourceRequirements(this AppointmentType appointmentType)
    {
        ArgumentNullException.ThrowIfNull(appointmentType);

        var requirements = (appointmentType.ResourceRequirements ?? [])
            .Where(requirement => requirement?.ResourceIds?.Any(id => !string.IsNullOrEmpty(id)) == true)
            .Select(requirement => new AppointmentResourceRequirement
            {
                Name = requirement.Name,
                ResourceIds = requirement.ResourceIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList(),
                Assignment = requirement.Assignment,
                Weights = requirement.Weights ?? new Dictionary<string, int>(StringComparer.Ordinal),
            })
            .ToList();

        if (requirements.Count > 0)
        {
            return requirements;
        }

        return
        [
            new AppointmentResourceRequirement
            {
                ResourceIds = (appointmentType.ResourceIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList(),
                Assignment = appointmentType.Assignment,
            },
        ];
    }

    /// <summary>
    /// Gets every resource any requirement of the type can take.
    /// </summary>
    /// <param name="appointmentType">The appointment type.</param>
    /// <returns>The resource ids, without duplicates, in requirement order.</returns>
    public static IReadOnlyList<string> GetAllResourceIds(this AppointmentType appointmentType)
    {
        ArgumentNullException.ThrowIfNull(appointmentType);

        return appointmentType.GetResourceRequirements()
            .SelectMany(requirement => requirement.ResourceIds)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsOutsideNotice(int noticeMinutes, DateTime startUtc, DateTime nowUtc)
    {
        return noticeMinutes <= 0 || startUtc - nowUtc >= TimeSpan.FromMinutes(noticeMinutes);
    }
}
