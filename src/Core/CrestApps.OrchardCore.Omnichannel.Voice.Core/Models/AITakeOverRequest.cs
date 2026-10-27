namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// A person asked to take an AI call over. Kept on the activity so the handoff that follows rings that person
/// directly instead of seating the caller in the subject's queue.
/// </summary>
public sealed class AITakeOverRequest
{
    /// <summary>
    /// Gets or sets the agent profile of the person taking the call.
    /// </summary>
    public string TargetAgentId { get; set; }

    /// <summary>
    /// Gets or sets the user name of the person who asked.
    /// </summary>
    public string RequestedBy { get; set; }

    /// <summary>
    /// Gets or sets when they asked.
    /// </summary>
    public DateTime RequestedUtc { get; set; }
}
