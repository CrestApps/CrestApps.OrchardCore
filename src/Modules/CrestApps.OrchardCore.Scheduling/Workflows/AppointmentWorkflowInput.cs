using System.Globalization;
using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// Builds the workflow input and output values of appointments, as plain dictionaries.
/// </summary>
public static class AppointmentWorkflowInput
{
    /// <summary>
    /// Converts an appointment to a dictionary.
    /// </summary>
    /// <param name="appointment">The appointment.</param>
    /// <param name="type">Its type, or <see langword="null"/>.</param>
    /// <returns>The dictionary.</returns>
    public static Dictionary<string, object> ToDictionary(Appointment appointment, AppointmentType type)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        return new Dictionary<string, object>
        {
            ["Id"] = appointment.ItemId,
            ["AppointmentTypeId"] = appointment.AppointmentTypeId,
            ["AppointmentTypeName"] = type?.Name,
            ["ResourceIds"] = appointment.ResourceIds.ToList(),
            ["StartUtc"] = Format(appointment.StartUtc),
            ["EndUtc"] = Format(appointment.EndUtc),
            ["Status"] = appointment.Status.ToString(),
            ["CustomerName"] = appointment.CustomerName,
            ["CustomerEmail"] = appointment.CustomerEmail,
            ["CustomerPhone"] = appointment.CustomerPhone,
            ["UserId"] = appointment.UserId,
            ["Notes"] = appointment.Notes,
            ["CalendarEventId"] = appointment.CalendarEventId,
            ["ManageToken"] = appointment.ManageToken,
            ["Source"] = appointment.Source,
            ["CancelReason"] = appointment.CancelReason,
        };
    }

    /// <summary>
    /// Builds the input of an appointment event.
    /// </summary>
    /// <param name="appointment">The appointment.</param>
    /// <param name="type">Its type.</param>
    /// <returns>The input.</returns>
    public static Dictionary<string, object> ToInput(Appointment appointment, AppointmentType type)
    {
        var values = ToDictionary(appointment, type);
        var input = new Dictionary<string, object>(values)
        {
            ["Appointment"] = values,
            ["AppointmentId"] = appointment.ItemId,
        };

        return input;
    }

    /// <summary>
    /// Formats a UTC time as ISO 8601.
    /// </summary>
    /// <param name="utc">The time.</param>
    /// <returns>The text.</returns>
    public static string Format(DateTime utc)
    {
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }
}
