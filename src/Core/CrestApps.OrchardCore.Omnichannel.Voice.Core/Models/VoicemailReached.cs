namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// A durable marker stored on the automated voice activity's property bag once the call is known to have been
/// answered by voicemail or an answering machine.
/// </summary>
/// <remarks>
/// It outlives the provider event that detected it: the greeting is heard on one transcription, the message is left
/// on a later turn or once the line goes quiet for the tone, and the call is concluded after the hangup. Each of those
/// steps reads it to know that nobody on this call is the customer.
/// </remarks>
public sealed class VoicemailReached
{
    /// <summary>
    /// Gets or sets whether the assistant has left its message on the recording.
    /// </summary>
    public bool MessageLeft { get; set; }

    /// <summary>
    /// Gets or sets whether the provider heard the greeting end, which is the moment to leave the message.
    /// </summary>
    public bool GreetingEnded { get; set; }

    /// <summary>
    /// Gets or sets whether this was known only because the provider said a machine answered, rather than from
    /// the greeting itself or from the model.
    /// </summary>
    /// <remarks>
    /// Detection can be wrong. A call marked on the provider's word alone that nonetheless turned into a
    /// conversation is concluded as the conversation it was.
    /// </remarks>
    public bool DetectedByProvider { get; set; }
}
