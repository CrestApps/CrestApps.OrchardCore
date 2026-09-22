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
    /// Gets or sets the technical name of the deployment that carries a
    /// <see cref="ChatMode.Conversation"/> conversation. Empty inherits the site's default realtime
    /// deployment, and failing that the first realtime-capable one.
    /// </summary>
    /// <remarks>
    /// This names the model the conversation is carried by, never how it is carried. Whether the resolved
    /// deployment speaks natively or chains speech-to-text, chat and text-to-speech together is read off the
    /// deployment itself, so the profile never stores a transport choice the deployment could contradict.
    /// </remarks>
    public string ConversationDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the voice name, used by conversation mode whichever way the conversation is carried.
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
    /// Gets or sets the deployments that can carry a spoken conversation.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AvailableConversationDeployments { get; set; }

    /// <summary>
    /// Gets or sets the technical names of the realtime-capable deployments.
    /// </summary>
    /// <remarks>
    /// The editor mirrors the realtime slot's chain against these to work out which deployment an empty
    /// selection would resolve to, so the voice list it loads matches the model that will speak.
    /// </remarks>
    [BindNever]
    public string[] RealtimeDeploymentNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the site's default realtime deployment, the second link in that chain.
    /// </summary>
    [BindNever]
    public string DefaultRealtimeDeploymentName { get; set; }
}
