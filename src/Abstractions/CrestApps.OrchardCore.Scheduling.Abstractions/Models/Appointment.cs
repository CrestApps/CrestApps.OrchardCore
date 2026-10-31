using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// A booked appointment.
/// </summary>
public sealed class Appointment : CatalogItem
{
    /// <summary>
    /// Gets or sets the appointment type.
    /// </summary>
    public string AppointmentTypeId { get; set; }

    /// <summary>
    /// Gets or sets the resources the appointment takes.
    /// </summary>
    public IList<string> ResourceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets when it starts, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when it ends, in UTC.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets the customer's time zone, for messages.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the appointment's state.
    /// </summary>
    public AppointmentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when a hold runs out.
    /// </summary>
    public DateTime? HoldExpiresUtc { get; set; }

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
    /// Gets or sets the customer's user id, when the customer has an account.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets notes about the appointment.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the calendar event that holds the appointment's time.
    /// </summary>
    public string CalendarEventId { get; set; }

    /// <summary>
    /// Gets or sets the secret in the customer's manage link.
    /// </summary>
    public string ManageToken { get; set; }

    /// <summary>
    /// Gets or sets where it was booked: Admin, SelfService, Api, AI or Workflow.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets why it was cancelled.
    /// </summary>
    public string CancelReason { get; set; }

    /// <summary>
    /// Gets or sets when it was booked.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }
}
