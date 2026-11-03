namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Whether a handoff to a live agent can reach somebody now.
/// </summary>
public enum HandoffAvailability
{
    /// <summary>
    /// Somebody can take the conversation: the queue is open, or keeps no hours.
    /// </summary>
    Open,

    /// <summary>
    /// The queue is closed now, and a handoff books a callback for when it opens.
    /// </summary>
    CallbackOnly,

    /// <summary>
    /// The queue is closed now and a handoff would book nothing: nobody can take the conversation.
    /// </summary>
    Unavailable,
}
