namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// A start time that can be booked.
/// </summary>
public sealed class AppointmentSlot
{
    /// <summary>
    /// Gets or sets when it starts, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when it ends, in UTC.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets the resources free at that time.
    /// </summary>
    public IList<string> ResourceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how many places are left, for a group session.
    /// </summary>
    public int Remaining { get; set; }
}
