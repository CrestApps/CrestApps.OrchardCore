namespace CrestApps.OrchardCore.Scheduling.Api;

/// <summary>
/// A request to cancel an appointment.
/// </summary>
public sealed class AppointmentCancelInput
{
    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }
}
