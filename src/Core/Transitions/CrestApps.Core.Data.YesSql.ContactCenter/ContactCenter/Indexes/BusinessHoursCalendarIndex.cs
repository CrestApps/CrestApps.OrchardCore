using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter.Models;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Indexes;

/// <summary>
/// Represents the YesSql index used to query business-hours calendars.
/// </summary>
public sealed class BusinessHoursCalendarIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the unique calendar name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the calendar is enabled.
    /// </summary>
    public bool Enabled { get; set; }
}
