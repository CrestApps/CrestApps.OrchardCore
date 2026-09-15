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
    /// Gets or sets the voice name, used by conversation mode and by a realtime chat deployment.
    /// </summary>
    public string VoiceName { get; set; }

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
    /// Gets or sets the technical names of the realtime-capable deployments.
    /// </summary>
    /// <remarks>
    /// The editor compares the selected chat deployment against these to decide whether the profile is a
    /// speech-to-speech conversation, which is the same question
    /// <c>IAIDeploymentCapabilityService.IsRealtimeDeploymentAsync</c> answers on the server.
    /// </remarks>
    [BindNever]
    public string[] RealtimeDeploymentNames { get; set; } = [];
}
