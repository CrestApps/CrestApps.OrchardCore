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
    /// Gets or sets the phone number to transfer the caller to, instead of an agent.
    /// </summary>
    public string TargetPhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the user name of the person who asked.
    /// </summary>
    public string RequestedBy { get; set; }

    /// <summary>
    /// Gets or sets when they asked.
    /// </summary>
    public DateTime RequestedUtc { get; set; }

    /// <summary>
    /// Gets or sets how a transfer to <see cref="TargetPhoneNumber"/> is made.
    /// </summary>
    public AICallTransferMode TransferMode { get; set; }

    /// <summary>
    /// Gets or sets what the person answering a warm or screened transfer is told about the caller. Empty has the AI
    /// write it from the conversation.
    /// </summary>
    public string Briefing { get; set; }

    /// <summary>
    /// Gets or sets the line the caller hears once connected on a warm or screened transfer, or empty for none.
    /// </summary>
    public string Introduction { get; set; }

    /// <summary>
    /// Gets or sets how long a warm or screened transfer rings before it is given up. Zero uses 30 seconds.
    /// </summary>
    public int RingTimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the audio the caller hears while a warm or screened transfer rings: a URL, or the name of audio
    /// the provider holds. Empty tells the caller to hold instead.
    /// </summary>
    public string HoldAudio { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the person rung sees the caller's number rather than ours, where the
    /// carrier allows it.
    /// </summary>
    public bool ShowCallerNumber { get; set; }
}
