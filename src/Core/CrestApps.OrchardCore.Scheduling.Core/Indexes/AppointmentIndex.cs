using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Scheduling.Core.Indexes;

/// <summary>
/// The YesSql index of <see cref="Appointment"/> documents.
/// </summary>
public sealed class AppointmentIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the appointment type.
    /// </summary>
    public string AppointmentTypeId { get; set; }

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

    /// <summary>
    /// Gets or sets when a hold runs out.
    /// </summary>
    public DateTime? HoldExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the customer's name, for searching.
    /// </summary>
    public string CustomerName { get; set; }

    /// <summary>
    /// Gets or sets the customer's email in lower case.
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
    /// Gets or sets when it was booked.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
