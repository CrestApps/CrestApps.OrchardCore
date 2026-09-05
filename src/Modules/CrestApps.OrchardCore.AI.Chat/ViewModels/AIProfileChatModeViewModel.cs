using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for AI profile chat mode.
/// </summary>
public class AIProfileChatModeViewModel
{
    /// <summary>
    /// Gets or sets the chat mode.
    /// </summary>
    public ChatMode ChatMode { get; set; }

    /// <summary>
    /// Gets or sets the voice name (used for Conversation and Realtime modes).
    /// </summary>
    public string VoiceName { get; set; }

    /// <summary>
    /// Gets or sets the realtime (speech-to-speech) deployment that backs the voice session. Empty uses
    /// the site's default realtime deployment.
    /// </summary>
    public string RealtimeDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether enable text to speech playback.
    /// </summary>
    public bool EnableTextToSpeechPlayback { get; set; }

    /// <summary>
    /// Gets or sets the available modes.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AvailableModes { get; set; }

    /// <summary>
    /// Gets or sets the available voices (text-to-speech voices for Conversation mode).
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AvailableVoices { get; set; }

    /// <summary>
    /// Gets or sets the realtime-capable deployments available for Realtime mode.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> RealtimeDeployments { get; set; } = [];

    /// <summary>
    /// Gets or sets whether a realtime-capable deployment is available.
    /// </summary>
    [BindNever]
    public bool HasRealtime { get; set; }
}
