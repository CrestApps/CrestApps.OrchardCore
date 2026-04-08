using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.ResponseHandling;

/// <summary>
/// Settings stored on <see cref="AIProfile.Settings"/> to control the initial
/// response handler for new chat sessions using this profile.
/// </summary>
/// <remarks>
/// When <see cref="InitialResponseHandlerName"/> is <see langword="null"/> or empty,
/// the default AI handler processes all prompts. Set this to a specific handler name
/// (e.g., <c>"Genesys"</c>) to route prompts to that handler from the start of the session.
/// The active handler can be changed mid-conversation by an AI function or external event
/// by updating <see cref="AIChatSession.ResponseHandlerName"/> or
/// <see cref="ChatInteraction.ResponseHandlerName"/>.
/// </remarks>
public sealed class ResponseHandlerProfileSettings
{
    /// <summary>
    /// Gets or sets the name of the initial <see cref="IChatResponseHandler"/>
    /// for new sessions created from this profile.
    /// When <see langword="null"/> or empty, the default AI handler is used.
    /// </summary>
    public string InitialResponseHandlerName { get; set; }
}
