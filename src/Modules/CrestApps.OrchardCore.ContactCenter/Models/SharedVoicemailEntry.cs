using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// A message as the shared voicemail list shows it: the message, with what the viewer may do with it.
/// </summary>
public sealed class SharedVoicemailEntry
{
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    public SharedVoicemail Voicemail { get; set; }

    /// <summary>
    /// Gets or sets the name of the message's queue.
    /// </summary>
    public string QueueName { get; set; }

    /// <summary>
    /// Gets or sets how long the message is, in seconds, when known.
    /// </summary>
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a recording was captured for the message.
    /// </summary>
    public bool HasRecording { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer is the one handling the message.
    /// </summary>
    public bool IsMine { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer may claim the message.
    /// </summary>
    public bool CanClaim { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer may return the message to the team.
    /// </summary>
    public bool CanRelease { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer may mark the message as dealt with.
    /// </summary>
    public bool CanResolve { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer may have the caller called back.
    /// </summary>
    public bool CanCallBack { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer may delete the message.
    /// </summary>
    public bool CanDelete { get; set; }
}
