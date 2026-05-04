using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for post session task.
/// </summary>
public class PostSessionTaskViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public PostSessionTaskType Type { get; set; }

    /// <summary>
    /// Gets or sets the instructions.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether allow multiple values.
    /// </summary>
    public bool AllowMultipleValues { get; set; }

    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    public List<PostSessionTaskOptionViewModel> Options { get; set; } = [];
}

/// <summary>
/// Represents the view model for post session task option.
/// </summary>
public class PostSessionTaskOptionViewModel
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }
}
