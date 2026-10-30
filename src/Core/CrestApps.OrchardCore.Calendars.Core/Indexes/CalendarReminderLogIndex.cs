using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Calendars.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Calendars.Core.Indexes;

/// <summary>
/// The YesSql index of <see cref="CalendarReminderLog"/> documents.
/// </summary>
public sealed class CalendarReminderLogIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the reminder's key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets when the reminder was raised.
    /// </summary>
    public DateTime RaisedUtc { get; set; }
}
