using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Core.Indexes;

/// <summary>
/// The YesSql index of <see cref="Calendar"/> documents.
/// </summary>
public sealed class CalendarIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the calendar's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the calendar's type.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets who the calendar belongs to.
    /// </summary>
    public CalendarOwnerKind OwnerKind { get; set; }

    /// <summary>
    /// Gets or sets the owner's id: a user id or a role name.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the calendar's visibility.
    /// </summary>
    public CalendarVisibility Visibility { get; set; }

    /// <summary>
    /// Gets or sets the module that manages the calendar.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the record in the managing module.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the secret part of the calendar's feed address.
    /// </summary>
    public string FeedToken { get; set; }

    /// <summary>
    /// Gets or sets whether the calendar is in use.
    /// </summary>
    public bool Enabled { get; set; }
}
