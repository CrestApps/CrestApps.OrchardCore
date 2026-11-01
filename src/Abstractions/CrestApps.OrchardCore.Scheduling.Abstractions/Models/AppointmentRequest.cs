namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// A request to book an appointment.
/// </summary>
public sealed class AppointmentRequest
{
    /// <summary>
    /// Gets or sets the appointment type.
    /// </summary>
    public string AppointmentTypeId { get; set; }

    /// <summary>
    /// Gets or sets when it starts, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets the resource the customer chose, or null to let the type pick.
    /// </summary>
    public string ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the customer's name.
    /// </summary>
    public string CustomerName { get; set; }

    /// <summary>
    /// Gets or sets the customer's email.
    /// </summary>
    public string CustomerEmail { get; set; }

    /// <summary>
    /// Gets or sets the customer's phone number.
    /// </summary>
    public string CustomerPhone { get; set; }

    /// <summary>
    /// Gets or sets the customer's user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets notes.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the customer's time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets where it is booked from.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets who books it.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets whether to only hold the time for the type's hold minutes.
    /// </summary>
    public bool Hold { get; set; }

    /// <summary>
    /// Gets or sets whether the customer books it themselves, such as on the public booking page or through an AI assistant,
    /// so the type's customer booking rules apply. Staff who manage appointments leave it off.
    /// </summary>
    public bool SelfService { get; set; }
}
