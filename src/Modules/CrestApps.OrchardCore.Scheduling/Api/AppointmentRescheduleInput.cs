namespace CrestApps.OrchardCore.Scheduling.Api;

/// <summary>
/// A request to move an appointment.
/// </summary>
public sealed class AppointmentRescheduleInput
{
    /// <summary>
    /// Gets or sets the new start.
    /// </summary>
    public DateTimeOffset StartUtc { get; set; }

    /// <summary>
    /// Gets or sets a resource to move it to, or <see langword="null"/>.
    /// </summary>
    public string ResourceId { get; set; }
}
