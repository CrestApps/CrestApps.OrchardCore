using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The category editor.
/// </summary>
public class CalendarCategoryEditViewModel
{
    /// <summary>
    /// Gets or sets the category's id, or empty for a new one.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets whether everyone can use the category.
    /// </summary>
    public bool Shared { get; set; }

    /// <summary>
    /// Gets or sets whether the user may make categories everyone can use.
    /// </summary>
    [BindNever]
    public bool CanShare { get; set; }
}
