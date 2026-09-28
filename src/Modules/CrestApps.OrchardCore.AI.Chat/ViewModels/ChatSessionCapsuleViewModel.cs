using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for chat session capsule.
/// </summary>
public class ChatSessionCapsuleViewModel
{
    /// <summary>
    /// Gets or sets the session.
    /// </summary>
    public AIChatSession Session { get; set; }

    /// <summary>
    /// Gets or sets the profile.
    /// </summary>
    public AIProfile Profile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether is new.
    /// </summary>
    [BindNever]
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the session is shown as a read-only transcript.
    /// </summary>
    /// <remarks>
    /// A system-owned session (an automated SMS or voice conversation, which carries no user) is reviewed
    /// through the resource that owns it, not continued. The page renders its history itself and neither
    /// offers a composer nor asks the chat hub to load it: the hub only loads the caller's own sessions.
    /// </remarks>
    [BindNever]
    public bool IsReadOnly { get; set; }
}
