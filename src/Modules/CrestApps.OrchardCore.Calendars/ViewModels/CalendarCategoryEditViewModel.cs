using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement;

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

    /// <summary>
    /// Gets or sets the editor, built by the display drivers of this view model.
    /// </summary>
    [BindNever]
    public IShape Editor { get; set; }
}
