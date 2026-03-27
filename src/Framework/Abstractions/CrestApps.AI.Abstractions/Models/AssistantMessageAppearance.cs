namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Describes how an assistant message should be presented in the chat UI.
/// </summary>
public sealed class AssistantMessageAppearance
{
    /// <summary>
    /// Gets or sets the assistant role label shown in the chat UI.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the Font Awesome icon classes for the assistant message.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Gets or sets the CSS class applied to the assistant role label and icon.
    /// </summary>
    public string CssClass { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the streaming animation should be disabled.
    /// </summary>
    public bool DisableStreamingAnimation { get; set; }
}
