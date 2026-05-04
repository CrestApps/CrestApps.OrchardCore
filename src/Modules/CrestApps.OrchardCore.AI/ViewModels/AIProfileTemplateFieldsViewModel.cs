namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for AI profile template fields.
/// </summary>
public class AIProfileTemplateFieldsViewModel
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether is listable.
    /// </summary>
    public bool IsListable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether is new.
    /// </summary>
    public bool IsNew { get; set; }
}
