namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// A category people put events in to color-code their calendar and filter it: a user's own, or one everyone can use.
/// </summary>
public sealed class CalendarCategory
{
    /// <summary>
    /// Gets or sets the category's id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the category's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the color of the category's events, such as <c>#2f9e44</c>.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets the user the category belongs to, or <see langword="null"/> for a category everyone can use.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets when the category was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets whether everyone can use the category.
    /// </summary>
    public bool IsShared => string.IsNullOrEmpty(OwnerId);
}
