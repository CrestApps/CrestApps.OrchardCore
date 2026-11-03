using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Scheduling.Core.Indexes;

/// <summary>
/// The YesSql index of <see cref="WaitlistEntry"/> documents.
/// </summary>
public sealed class WaitlistEntryIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the appointment type.
    /// </summary>
    public string AppointmentTypeId { get; set; }

    /// <summary>
    /// Gets or sets the start of the wanted times.
    /// </summary>
    public DateTime FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the end of the wanted times.
    /// </summary>
    public DateTime ToUtc { get; set; }

    /// <summary>
    /// Gets or sets the entry's state.
    /// </summary>
    public WaitlistStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the customer joined.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
