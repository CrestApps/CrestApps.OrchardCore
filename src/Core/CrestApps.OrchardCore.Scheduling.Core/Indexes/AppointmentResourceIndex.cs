using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Scheduling.Core.Indexes;

/// <summary>
/// One row per resource of an appointment, so a resource's appointments can be found by time.
/// </summary>
public sealed class AppointmentResourceIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the appointment.
    /// </summary>
    public string AppointmentId { get; set; }

    /// <summary>
    /// Gets or sets the appointment type.
    /// </summary>
    public string AppointmentTypeId { get; set; }

    /// <summary>
    /// Gets or sets the resource.
    /// </summary>
    public string ResourceId { get; set; }

    /// <summary>
    /// Gets or sets when it starts.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when it ends.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets its state.
    /// </summary>
    public AppointmentStatus Status { get; set; }
}
