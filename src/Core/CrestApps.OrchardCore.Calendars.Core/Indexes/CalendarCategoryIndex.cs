using YesSql.Indexes;

namespace CrestApps.OrchardCore.Calendars.Core.Indexes;

/// <summary>
/// Finds the categories a user can use.
/// </summary>
public sealed class CalendarCategoryIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the category's id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the user the category belongs to, or <see langword="null"/> for a shared one.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the category's name.
    /// </summary>
    public string Name { get; set; }
}
