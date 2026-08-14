using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// Represents the view model for chat interaction chat mode settings.
/// </summary>
public class ChatInteractionChatModeSettingsViewModel
{
    /// <summary>
    /// Gets or sets the chat mode.
    /// </summary>
    public ChatMode ChatMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether enable text to speech playback.
    /// </summary>
    public bool EnableTextToSpeechPlayback { get; set; }

    /// <summary>
    /// Gets or sets the available modes.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AvailableModes { get; set; }
}
