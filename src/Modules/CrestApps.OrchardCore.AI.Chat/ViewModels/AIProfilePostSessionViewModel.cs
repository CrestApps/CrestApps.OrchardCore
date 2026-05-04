namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for AI profile post session.
/// </summary>
public class AIProfilePostSessionViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether enable post session processing.
    /// </summary>
    public bool EnablePostSessionProcessing { get; set; }

    /// <summary>
    /// Gets or sets the tasks.
    /// </summary>
    public List<PostSessionTaskViewModel> Tasks { get; set; } = [];

    /// <summary>
    /// Gets or sets the post session tools.
    /// </summary>
    public Dictionary<string, PostSessionToolEntry[]> PostSessionTools { get; set; } = [];
}

/// <summary>
/// Represents the post session tool entry.
/// </summary>
public class PostSessionToolEntry
{
    /// <summary>
    /// Gets or sets the item id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether is selected.
    /// </summary>
    public bool IsSelected { get; set; }
}
