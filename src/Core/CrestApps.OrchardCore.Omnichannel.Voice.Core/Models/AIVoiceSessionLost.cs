namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// A durable marker stored on the automated voice activity's property bag when the assistant's live session was
/// lost partway through the call and could not be brought back.
/// </summary>
/// <remarks>
/// The call is concluded later, from the hangup, by a review that reads only the transcript. A transcript that
/// stops mid-conversation reads to it like a customer who lost interest, and one such call was concluded as
/// "Done" and never tried again. This is what tells the conclusion the conversation was cut short by us.
/// </remarks>
public sealed class AIVoiceSessionLost
{
    /// <summary>
    /// Gets or sets when the session was lost.
    /// </summary>
    public DateTime? LostUtc { get; set; }
}
